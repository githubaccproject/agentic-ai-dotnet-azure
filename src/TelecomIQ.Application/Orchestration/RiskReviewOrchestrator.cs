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
        if (review.Status == "Completed")
            return;

        var doc = await repository.Get(contractId, ct);
        if (doc?.Status != ProcessingStatus.Ready)
            throw new InvalidOperationException("Contract not ready");

        if (!await repository.UpdateReviewProgress(review with { Status = "Processing" }, ct))
            return;

        try
        {
            var results = await Task.WhenAll(ReviewTopics.Select(topic => review.Policies.Length == 0
                ? agent.Run(contractId, topic.Category, topic.Query, ct)
                : policyAgent.Run(contractId, topic.Category, topic.Query, review.Policies, ct)));

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
                Findings = results.SelectMany(result => result).ToArray(),
                RevisionChecks = checks.ToArray()
            }, ct);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await repository.UpdateReviewProgress(review with { Status = "Failed" }, ct);
            throw;
        }
    }
}
