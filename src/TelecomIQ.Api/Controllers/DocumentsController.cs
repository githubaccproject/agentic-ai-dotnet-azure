using Microsoft.AspNetCore.Mvc;
using TelecomIQ.Api.Contracts;
using TelecomIQ.Application.Services;

namespace TelecomIQ.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public sealed class DocumentsController(IContractService contracts) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType(typeof(ContractResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponse>> Upload(
        [FromForm] UploadContractRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (!PdfUpload.HasSupportedNameAndSize(file))
            return Problem(statusCode: 400, detail: "PDF up to 20 MB required.");

        await using var stream = file.OpenReadStream();
        if (!await PdfUpload.HasValidHeader(stream, cancellationToken))
            return Problem(statusCode: 400, detail: "Invalid PDF header.");

        var contract = await contracts.UploadAsync(stream, request.PreviousContractId, cancellationToken);
        if (contract is null) return Problem(statusCode: 404, detail: "Previous agreement not found.");
        return AcceptedAtAction(nameof(GetById), new { id = contract.Id }, ContractResponse.From(contract));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contract = await contracts.GetAsync(id, cancellationToken);
        if (contract is null) return NotFound();
        return Ok(ContractResponse.From(contract));
    }
}
