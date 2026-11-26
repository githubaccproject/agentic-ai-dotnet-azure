using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TelecomIQ.Application;
using TelecomIQ.Domain;

namespace TelecomIQ.Infrastructure;

// The scope is supplied by the trusted worker, never by the model.
public sealed record ReviewToolScope(Guid ReviewId);

public static class ReviewToolServer
{
    public static async Task Run(string scopeId)
    {
        if (!Guid.TryParse(scopeId, out var id) || id == Guid.Empty)
            throw new ArgumentException("A review scope is required.");
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new ReviewToolScope(id));
        builder.Services.AddTelecomIQ(builder.Configuration);
        builder.Services.AddMcpServer().WithStdioServerTransport()
            .WithTools<ReviewDataTools>().WithTools<FinancialReviewTools>();
        await builder.Build().RunAsync();
    }
}

[McpServerToolType]
public sealed class ReviewDataTools(ReviewToolScope scope, IRepository repository, IPolicySearch search, IAi ai)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<Review> Current(CancellationToken ct) =>
        await repository.GetReview(scope.ReviewId, ct) ?? throw new InvalidOperationException("Review scope not found.");

    [McpServerTool(Name = "get_policy_requirement"), Description("Search only policy versions selected for this review; returns policy evidence IDs and text.")]
    public async Task<string> GetPolicyRequirement(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 400)
            throw new ArgumentException("Query must contain 1-400 characters.");
        var review = await Current(ct);
        if (review.Policies.Length == 0) return "[]";
        var passages = await search.Retrieve(review.Policies, query, await ai.Embed(query, ct), ct);
        return JsonSerializer.Serialize(passages.Where(p => review.Policies.Contains(new PolicyReference(p.PolicyId, p.Version))).Take(8), JsonOptions);
    }

    [McpServerTool(Name = "get_review_decisions"), Description("Read decisions from the linked previous review, if any. No arbitrary review IDs are accepted.")]
    public async Task<string> GetReviewDecisions(CancellationToken ct)
    {
        var review = await Current(ct);
        if (review.PreviousReviewId is not Guid priorId) return "[]";
        var prior = await repository.GetReview(priorId, ct);
        if (prior is null || prior.Status != "Completed") return "[]";
        var contract = await repository.Get(review.ContractId, ct);
        if (contract?.PreviousContractId != prior.ContractId) return "[]";
        return JsonSerializer.Serialize(prior.Decisions.TakeLast(20).Select(d => new
        {
            d.FindingId, d.Action, d.RecordedAtUtc,
            Reason = d.Reason[..Math.Min(d.Reason.Length, 500)]
        }), JsonOptions);
    }
}

[McpServerToolType]
public sealed class FinancialReviewTools
{
    [McpServerTool(Name = "calculate_liability"), Description("Calculate a liability cap as annual contract value multiplied by months divided by twelve. Does not establish contract value or policy.")]
    public static string CalculateLiability(decimal annualContractValue, int months)
    {
        if (annualContractValue <= 0 || annualContractValue > 100000000000000m || months < 1 || months > 120)
            throw new ArgumentException("Amount and months are outside the supported range.");
        return JsonSerializer.Serialize(new { annualContractValue, months,
            liabilityCap = decimal.Round(annualContractValue * months / 12m, 2, MidpointRounding.AwayFromZero) });
    }
}
