using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TelecomIQ.Api.Contracts;
using TelecomIQ.Application.Services;
using TelecomIQ.Domain;

namespace TelecomIQ.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class RiskReviewController(IRiskReviewService reviews) : ControllerBase
{
    [HttpPost("contracts/{id:guid}/reviews")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> Submit(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubmitReviewRequest? request, CancellationToken ct)
    {
        var result = await reviews.SubmitAsync(id, request?.Policies ?? [], request?.PreviousReviewId, ct);
        switch (result.Status)
        {
            case ReviewSubmissionStatus.ContractNotFound:
                return NotFound();
            case ReviewSubmissionStatus.ContractNotReady:
                return Problem(statusCode: 409, detail: "Contract not ready.");
            case ReviewSubmissionStatus.InvalidPolicies:
                return Problem(statusCode: 409,
                    detail: $"Select up to {RiskReviewService.MaxPoliciesPerReview} ready policy versions, with one version per policy.");
            case ReviewSubmissionStatus.InvalidPreviousReview:
                return Problem(statusCode: 409,
                    detail: "Select a completed policy review of the agreement this revision was uploaded against. The re-review uses its policy versions.");
        }

        var review = result.Review!;
        return AcceptedAtAction(nameof(GetById), new { id = review.Id }, ReviewResponse.From(review));
    }

    [HttpGet("reviews/{id:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetById(Guid id, CancellationToken ct)
    {
        var review = await reviews.GetAsync(id, ct);
        if (review is null) return NotFound();
        return Ok(ReviewResponse.From(review));
    }

    [HttpPut("reviews/{id:guid}/decisions/{decisionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordDecision(Guid id, Guid decisionId, FindingDecisionRequest request, CancellationToken ct)
    {
        if (decisionId == Guid.Empty || request.FindingId == Guid.Empty)
            return Problem(statusCode: 400, detail: "Finding and decision IDs are required.");

        var decision = new FindingDecision(decisionId, request.FindingId, request.Action,
            request.Reviewer.Trim(), request.Reason.Trim(), DateTime.UtcNow)
        {
            ProposedWording = request.Action == "RequestChange" ? request.ProposedWording?.Trim() : null
        };
        return (await reviews.RecordDecision(id, decision, ct)) switch
        {
            DecisionStatus.Saved => NoContent(),
            DecisionStatus.FindingNotFound => NotFound(),
            DecisionStatus.ReviewNotCompleted => Problem(statusCode: 409, detail: "Wait for the review to complete."),
            _ => Problem(statusCode: 409, detail: "That decision ID was already used for a different decision.")
        };
    }
}
