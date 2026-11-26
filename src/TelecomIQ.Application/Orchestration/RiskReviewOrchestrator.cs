using System.Net;
using TelecomIQ.Application.Agents;
using TelecomIQ.Domain;

namespace TelecomIQ.Application.Orchestration;

public sealed class RiskReviewOrchestrator(IRepository repository, RiskAgent agent, PolicyReviewAgent policyAgent)
{
    private static readonly (string Category, string Query)[] ReviewTopics =
    [
        ("Vendor Legal", "vendor indemnification liability caps termination exit assistance intellectual property subcontracting dispute resolution"),
        ("Commercial", "vendor pricing annual escalation payment terms integration fees service credits penalties renewal charges"),
        ("Telecom Security & Compliance", "network availability SLA outage disaster recovery security incident notification reporting deadline hours customer data privacy retention cross-border transfers audit access encryption")
    ];

    public async Task Run(Guid contractId, Guid reviewId, CancellationToken ct)
    {
        var review = await repository.GetReview(reviewId, ct) ?? throw new InvalidOperationException("Unknown review");
        if (review.ContractId != contractId)
            throw new InvalidOperationException("Contract mismatch");
        if (review.Status == "Completed" || review.FailureCode == Review.Throttled)
            return;

        var doc = await repository.Get(contractId, ct);
        if (doc?.Status != ProcessingStatus.Ready)
            throw new InvalidOperationException("Contract not ready");

        if (!await repository.UpdateReviewProgress(review with { Status = "Processing" }, ct))
            return;

        try
        {
            var findings = new List<Finding>();
            var activity = new List<ToolActivity>();
            foreach (var topic in ReviewTopics)
            {
                var result = review.Policies.Length == 0
                    ? await agent.Run(contractId, topic.Category, topic.Query, ct)
                    : await policyAgent.Run(contractId, topic.Category, topic.Query, review.Policies, ct, review.Id, activity);
                findings.AddRange(result);
            }

            var checks = new List<RevisionCheck>();
            if (review.PreviousReviewId is Guid previousId)
            {
                var previous = await repository.GetReview(previousId, ct)
                    ?? throw new InvalidOperationException("Previous review not found");

                foreach (var finding in previous.Findings)
                {
                    var check = await policyAgent.Recheck(contractId, finding, ct);
                    if (check is not null)
                        checks.Add(check);
                }
            }

            await repository.UpdateReviewProgress(review with
            {
                Status = "Completed",
                Findings = findings.ToArray(),
                RevisionChecks = checks.ToArray(),
                ToolActivity = activity.ToArray()
            }, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && !ct.IsCancellationRequested)
        {
            // Finish the delivery after saving the failure so Service Bus does not replay all calls immediately.
            await repository.UpdateReviewProgress(review with { Status = "Failed", FailureCode = Review.Throttled }, ct);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await repository.UpdateReviewProgress(review with { Status = "Failed" }, ct);
            throw;
        }
    }
}
