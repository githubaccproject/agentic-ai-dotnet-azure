using Microsoft.AspNetCore.Mvc;
using ContractIQ.Api.Contracts;
using ContractIQ.Application.Services;

namespace ContractIQ.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class RiskReviewController(IRiskReviewService reviews) : ControllerBase
{
    [HttpPost("contracts/{id:guid}/reviews")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await reviews.SubmitAsync(id, cancellationToken);
        if (result.Status == ReviewSubmissionStatus.ContractNotFound) return NotFound();
        if (result.Status == ReviewSubmissionStatus.ContractNotReady)
            return Problem(statusCode: 409, detail: "Contract not ready.");
        var review = result.Review!;
        return AcceptedAtAction(nameof(GetById), new { id = review.Id }, ReviewResponse.From(review));
    }

    [HttpGet("reviews/{id:guid}")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var review = await reviews.GetAsync(id, cancellationToken);
        if (review is null) return NotFound();
        return Ok(ReviewResponse.From(review));
    }
}
