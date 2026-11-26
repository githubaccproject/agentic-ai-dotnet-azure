using TelecomIQ.Domain;

namespace TelecomIQ.Application.Services;

public enum ReviewSubmissionStatus
{
    Accepted, ContractNotFound, ContractNotReady, InvalidPolicies, InvalidPreviousReview
}

public enum DecisionStatus { Saved, FindingNotFound, ReviewNotCompleted, Conflict }

public sealed record ReviewSubmission(ReviewSubmissionStatus Status, Review? Review = null);

public interface IRiskReviewService
{
    Task<ReviewSubmission> SubmitAsync(Guid contractId, PolicyReference[] policies, Guid? previousReviewId, CancellationToken ct);
    Task<Review?> GetAsync(Guid id, CancellationToken ct);
    Task<DecisionStatus> RecordDecision(Guid reviewId, FindingDecision decision, CancellationToken ct);
}

public sealed class RiskReviewService(IRepository repository, IPolicyRepository policies, IQueue queue) : IRiskReviewService
{
    public const int MaxPoliciesPerReview = 12;

    public async Task<ReviewSubmission> SubmitAsync(
        Guid contractId, PolicyReference[] selected, Guid? previousReviewId, CancellationToken ct)
    {
        var contract = await repository.Get(contractId, ct);
        if (contract is null)
            return new(ReviewSubmissionStatus.ContractNotFound);
        if (contract.Status != ProcessingStatus.Ready)
            return new(ReviewSubmissionStatus.ContractNotReady);

        if (previousReviewId is Guid priorId)
        {
            var previous = await repository.GetReview(priorId, ct);
            if (previous is null || previous.Status != "Completed" || previous.Policies.Length == 0
                || contract.PreviousContractId != previous.ContractId || selected.Length != 0)
                return new(ReviewSubmissionStatus.InvalidPreviousReview);

            selected = previous.Policies;
        }

        if (selected.Length > MaxPoliciesPerReview
            || selected.Any(p => p is null || p.PolicyId == Guid.Empty || p.Version < 1)
            || selected.Select(p => p.PolicyId).Distinct().Count() != selected.Length)
            return new(ReviewSubmissionStatus.InvalidPolicies);

        foreach (var selectedVersion in selected)
        {
            var policy = await policies.Get(PolicyVersion.Key(selectedVersion.PolicyId, selectedVersion.Version), ct);
            if (policy?.Status != PolicyIngestionStatus.Ready)
                return new(ReviewSubmissionStatus.InvalidPolicies);
        }

        var review = new Review(Guid.NewGuid(), contractId, "Queued", [])
        {
            Policies = selected,
            PreviousReviewId = previousReviewId
        };
        await repository.SaveReview(review, ct);
        try
        {
            await queue.Send(new Job("review", contractId, review.Id), ct);
        }
        catch
        {
            await repository.UpdateReviewProgress(review with { Status = "Failed" }, CancellationToken.None);
            throw;
        }

        return new(ReviewSubmissionStatus.Accepted, review);
    }

    public Task<Review?> GetAsync(Guid id, CancellationToken ct) => repository.GetReview(id, ct);

    public async Task<DecisionStatus> RecordDecision(Guid reviewId, FindingDecision decision, CancellationToken ct)
    {
        if (await repository.AppendDecision(reviewId, decision, ct))
            return DecisionStatus.Saved;

        var review = await repository.GetReview(reviewId, ct);
        if (review is null)
            return DecisionStatus.FindingNotFound;
        if (review.Status != "Completed")
            return DecisionStatus.ReviewNotCompleted;
        if (!review.Findings.Any(f => f.Id == decision.FindingId))
            return DecisionStatus.FindingNotFound;

        var existing = review.Decisions.SingleOrDefault(d => d.Id == decision.Id);
        return existing is not null && (existing with { RecordedAtUtc = decision.RecordedAtUtc }) == decision
            ? DecisionStatus.Saved : DecisionStatus.Conflict;
    }
}
