using System.ComponentModel.DataAnnotations;
using TelecomIQ.Domain;

namespace TelecomIQ.Api.Contracts;

public sealed record SubmitReviewRequest(PolicyReference[]? Policies, Guid? PreviousReviewId);

public sealed class FindingDecisionRequest
{
    public Guid FindingId { get; init; }

    [Required]
    [RegularExpression("AcceptRisk|RequestChange|ApproveException")]
    public string Action { get; init; } = "";

    [Required, StringLength(100)]
    public string Reviewer { get; init; } = "";

    [Required, StringLength(500)]
    public string Reason { get; init; } = "";

    [StringLength(1500)]
    public string? ProposedWording { get; init; }
}
