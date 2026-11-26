using System.Net;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using TelecomIQ.Application;
using TelecomIQ.Application.Agents;
using TelecomIQ.Application.Orchestration;
using TelecomIQ.Application.RAG;
using TelecomIQ.Application.Services;
using TelecomIQ.Domain;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
var sample = new Review(Guid.NewGuid(), Guid.NewGuid(), "Completed", []);
var legacyRow = sample.ToBsonDocument();
foreach (var field in new[] { "Policies", "PreviousReviewId", "RevisionChecks", "Decisions", "FailureCode" })
    legacyRow.Remove(field);
var legacy = BsonSerializer.Deserialize<Review>(legacyRow);
Check(legacy.Policies.Length == 0 && legacy.Decisions.Length == 0 && legacy.RevisionChecks.Length == 0,
    "Existing MongoDB reviews should load without the new fields.");

var store = new ReviewStore();
var queue = new ReviewQueue();
var agreement = new Contract(Guid.NewGuid(), "original.pdf", ProcessingStatus.Ready);
var revised = new Contract(Guid.NewGuid(), "revision.pdf", ProcessingStatus.Ready) { PreviousContractId = agreement.Id };
var unrelated = new Contract(Guid.NewGuid(), "unrelated.pdf", ProcessingStatus.Ready);
foreach (var contract in new[] { agreement, revised, unrelated })
    store.Contracts.Add(contract.Id, contract);

var policyId = Guid.NewGuid();
var policyV1 = new PolicyReference(policyId, 1);
var policyV2 = new PolicyReference(policyId, 2);
store.AddPolicy(policyV1, PolicyIngestionStatus.Ready);
store.AddPolicy(policyV2, PolicyIngestionStatus.Pending);
var service = new RiskReviewService(store, store, queue);
var ct = CancellationToken.None;
Check((await service.SubmitAsync(agreement.Id, [policyV2], null, ct)).Status == ReviewSubmissionStatus.InvalidPolicies,
    "Ingestion must complete before a version can be selected.");
Check((await service.SubmitAsync(agreement.Id, [policyV1, policyV1], null, ct)).Status == ReviewSubmissionStatus.InvalidPolicies,
    "Only one version per policy can be selected.");
Check(queue.Jobs.Count == 0, "Invalid requests should not queue reviews.");

var first = (await service.SubmitAsync(agreement.Id, [policyV1], null, ct)).Review!;
Check(first.Policies.SequenceEqual([policyV1]) && queue.Jobs.Single().ReviewId == first.Id,
    "The review should pin the requested version before queueing.");

var providers = new EvidenceProviders(agreement.Id, policyId);
var rag = new RagService(providers, providers, providers);
var policyAgent = new PolicyReviewAgent(rag, providers);
var worker = new RiskReviewOrchestrator(store, new RiskAgent(rag, providers), policyAgent);
await worker.Run(agreement.Id, first.Id, ct);
first = (await store.GetReview(first.Id, ct))!;
var finding = first.Findings.Single();
Check(first.Status == "Completed" && finding.Id.HasValue && finding.ContractEvidence?.Page == 2
    && finding.PolicyEvidence?.Version == 1 && finding.PolicyEvidence.Page == 4
    && finding.SuggestedWording is not null,
    "Only the finding with citations from both pinned sources should survive validation.");

var roundTrip = BsonSerializer.Deserialize<Review>(first.ToBsonDocument());
Check(roundTrip.Findings.Single().PolicyEvidence?.Id == "policy-v1", "Policy evidence must survive MongoDB serialization.");

var decision = new FindingDecision(Guid.NewGuid(), finding.Id!.Value, "RequestChange", "Security reviewer",
    "Report suspected or confirmed incidents within 24 hours of discovery.", DateTime.UtcNow);
Check(await service.RecordDecision(first.Id, decision, ct) == DecisionStatus.Saved, "The reviewer can record a decision.");
Check(await service.RecordDecision(first.Id, decision with { RecordedAtUtc = DateTime.UtcNow }, ct) == DecisionStatus.Saved,
    "Retrying the same decision must succeed.");
Check(await service.RecordDecision(first.Id, decision with { Reason = "Different decision" }, ct) == DecisionStatus.Conflict,
    "Reusing a decision ID for different content must fail.");

await worker.Run(agreement.Id, first.Id, ct);
Check((await store.GetReview(first.Id, ct))!.Decisions.Length == 1, "A repeated worker delivery must preserve decisions.");
Check((await service.SubmitAsync(unrelated.Id, [], first.Id, ct)).Status == ReviewSubmissionStatus.InvalidPreviousReview,
    "A revision review cannot refer to an unrelated agreement.");

store.AddPolicy(policyV2, PolicyIngestionStatus.Ready);
var reReview = (await service.SubmitAsync(revised.Id, [], first.Id, ct)).Review!;
Check(reReview.Policies.SequenceEqual([policyV1]), "Re-review must use the original policy even when a newer version is ready.");
await worker.Run(revised.Id, reReview.Id, ct);
reReview = (await store.GetReview(reReview.Id, ct))!;
var recheck = reReview.RevisionChecks.Single();
Check(reReview.Findings.Length == 0 && recheck.Status == "Resolved" && recheck.RevisedEvidence?.Id == "revision",
    "Resolved rechecks need a citation from the revised contract.");

providers.RevisionCitation = "invented-id";
var unsupported = await policyAgent.Recheck(revised.Id, finding, ct);
Check(unsupported?.Status == "NeedsReview" && unsupported.RevisedEvidence is null,
    "A resolution with an unknown citation must be sent to human review.");

var existingFlow = (await service.SubmitAsync(agreement.Id, [], null, ct)).Review!;
await worker.Run(agreement.Id, existingFlow.Id, ct);
Check((await store.GetReview(existingFlow.Id, ct))!.Status == "Completed",
    "Submitting an agreement-only review should still work.");

providers.ThrottleAgreementReview = true;
var throttled = (await service.SubmitAsync(agreement.Id, [], null, ct)).Review!;
var attemptsBeforeThrottle = providers.AgreementAnalysisAttempts;
await worker.Run(agreement.Id, throttled.Id, ct);
await worker.Run(agreement.Id, throttled.Id, ct);
var failed = (await store.GetReview(throttled.Id, ct))!;
Check(failed.Status == "Failed" && failed.FailureCode == Review.Throttled
    && providers.AgreementAnalysisAttempts == attemptsBeforeThrottle + 1,
    "Throttle exhaustion should end the review once without replaying other categories.");

await AzureThrottleChecks.Run();
Console.WriteLine("Policy review checks passed.");

static void Check(bool condition, string failure)
{
    if (!condition) throw new InvalidOperationException(failure);
}

sealed class ReviewQueue : IQueue
{
    public List<Job> Jobs { get; } = [];
    public Task Send(Job job, CancellationToken ct)
    {
        Jobs.Add(job);
        return Task.CompletedTask;
    }
}

sealed class ReviewStore : IRepository, IPolicyRepository
{
    public Dictionary<Guid, Contract> Contracts { get; } = [];
    private readonly Dictionary<Guid, Review> reviews = [];
    private readonly Dictionary<string, PolicyVersion> policies = [];

    public void AddPolicy(PolicyReference policy, PolicyIngestionStatus status)
    {
        var id = PolicyVersion.Key(policy.PolicyId, policy.Version);
        policies[id] = new(id, policy.PolicyId, policy.Version, "policy.pdf", "sample-hash", status);
    }

    public Task<Contract?> Get(Guid id, CancellationToken ct) => Task.FromResult(Contracts.GetValueOrDefault(id));
    public Task<PolicyVersion?> Get(string id, CancellationToken ct) => Task.FromResult(policies.GetValueOrDefault(id));
    public Task<Review?> GetReview(Guid id, CancellationToken ct) => Task.FromResult(reviews.GetValueOrDefault(id));

    public Task SaveReview(Review review, CancellationToken ct)
    {
        reviews.Add(review.Id, review);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateReviewProgress(Review review, CancellationToken ct)
    {
        var current = reviews[review.Id];
        if (current.Status == "Completed") return Task.FromResult(false);
        reviews[review.Id] = review with { Decisions = current.Decisions };
        return Task.FromResult(true);
    }

    public Task<bool> AppendDecision(Guid id, FindingDecision decision, CancellationToken ct)
    {
        var current = reviews.GetValueOrDefault(id);
        if (current?.Status != "Completed" || !current.Findings.Any(f => f.Id == decision.FindingId)
            || current.Decisions.Any(d => d.Id == decision.Id))
            return Task.FromResult(false);
        reviews[id] = current with { Decisions = [..current.Decisions, decision] };
        return Task.FromResult(true);
    }

    public Task Save(Contract contract, CancellationToken ct) => throw new NotSupportedException();
    public Task<PolicyVersion[]> ListReady(CancellationToken ct) => throw new NotSupportedException();
    public Task<PolicyVersion> Reserve(PolicyVersion policy, CancellationToken ct) => throw new NotSupportedException();
    public Task SetStatus(string id, PolicyIngestionStatus status, CancellationToken ct) => throw new NotSupportedException();
}

sealed class EvidenceProviders(Guid originalId, Guid policyId) : IAi, IPolicyReviewAi, ISearch, IPolicySearch
{
    public bool ThrottleAgreementReview { get; set; }
    public int AgreementAnalysisAttempts { get; private set; }
    public string RevisionCitation { get; set; } = "revision";

    public Task<float[]> Embed(string text, CancellationToken ct) => Task.FromResult<float[]>([1]);
    public Task<Finding[]> Analyze(string category, Evidence[] evidence, CancellationToken ct)
    {
        AgreementAnalysisAttempts++;
        if (ThrottleAgreementReview) throw new HttpRequestException("Rate limited.", null, HttpStatusCode.TooManyRequests);
        return Task.FromResult<Finding[]>([]);
    }

    public Task<Evidence[]> Retrieve(Guid id, string query, float[] vector, CancellationToken ct) =>
        Task.FromResult<Evidence[]>(id == originalId
            ? [new("original", 2, "Report incidents within 72 hours of confirmation.")]
            : [new("revision", 3, "Report suspected or confirmed incidents within 24 hours of discovery.")]);

    public Task<PolicyEvidence[]> Retrieve(PolicyReference[] policies, string query, float[] vector, CancellationToken ct) =>
        Task.FromResult<PolicyEvidence[]>(
        [
            new("policy-v1", policyId, 1, 4, "Report suspected or confirmed incidents within 24 hours of discovery."),
            new("policy-v2", policyId, 2, 7, "Report incidents within 12 hours.")
        ]);

    public Task<PolicyAnalysis[]> Compare(string category, Evidence[] contract, PolicyEvidence[] policy, CancellationToken ct) =>
        Task.FromResult<PolicyAnalysis[]>(category == "Telecom Security & Compliance" && contract[0].Id == "original"
            ?
            [
                new("High", "The reporting deadline exceeds the policy requirement.", "original", "policy-v1", policy.Single().Text),
                new("High", "The draft is slower than a newer version.", "original", "policy-v2", null),
                new("High", "This passage is fabricated.", "invented-id", "policy-v1", null)
            ]
            : []);

    public Task<RevisionAnalysis> Recheck(Finding previous, Evidence[] revised, CancellationToken ct) =>
        Task.FromResult(new RevisionAnalysis("Resolved", "The new clause meets the deadline.", RevisionCitation));

    public Task Index(Chunk chunk, CancellationToken ct) => throw new NotSupportedException();
    public Task Index(PolicyChunk chunk, CancellationToken ct) => throw new NotSupportedException();
}
