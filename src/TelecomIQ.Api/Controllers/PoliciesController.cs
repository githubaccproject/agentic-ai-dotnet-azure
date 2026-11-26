using Microsoft.AspNetCore.Mvc;
using TelecomIQ.Api.Contracts;
using TelecomIQ.Application.Services;
using TelecomIQ.Application;

namespace TelecomIQ.Api.Controllers;

[ApiController]
[Route("api/policies/{policyId:guid}/versions/{version:int}")]
public sealed class PoliciesController(PolicyUploadService policies, IPolicyRepository repository) : ControllerBase
{
    [HttpGet("/api/policies")]
    [ProducesResponseType(typeof(PolicyVersionResponse[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyVersionResponse[]>> ListReady(CancellationToken ct) =>
        Ok((await repository.ListReady(ct)).Select(PolicyVersionResponse.From).ToArray());

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType(typeof(PolicyVersionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PolicyVersionResponse>> Upload(
        Guid policyId, int version, [FromForm] UploadPolicyRequest request, CancellationToken ct)
    {
        if (policyId == Guid.Empty || version < 1)
            return Problem(statusCode: 400, detail: "A non-empty policy ID and positive version are required.");

        var file = request.File;
        if (!PdfUpload.HasSupportedNameAndSize(file))
            return Problem(statusCode: 400, detail: "PDF up to 20 MB required.");

        await using var stream = file.OpenReadStream();
        if (!await PdfUpload.HasValidHeader(stream, ct))
            return Problem(statusCode: 400, detail: "Invalid PDF header.");

        var policy = await policies.Upload(policyId, version, stream, ct);
        if (policy is null)
            return Problem(statusCode: 409, detail: "This policy version already has different content. Use a new version.");

        return AcceptedAtAction(nameof(GetByVersion), new { policyId, version }, PolicyVersionResponse.From(policy));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PolicyVersionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyVersionResponse>> GetByVersion(Guid policyId, int version, CancellationToken ct)
    {
        var policy = await policies.Get(policyId, version, ct);
        if (policy is null)
            return NotFound();

        return Ok(PolicyVersionResponse.From(policy));
    }
}
