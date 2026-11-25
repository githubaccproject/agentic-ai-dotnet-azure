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
    public async Task<ActionResult<ContractResponse>> Upload(
        [FromForm] UploadContractRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file.Length < 5 || file.Length > 20 * 1024 * 1024 ||
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Problem(statusCode: 400, detail: "PDF up to 20 MB required.");

        await using var stream = file.OpenReadStream();
        var header = new byte[5];
        if (await stream.ReadAtLeastAsync(header, 5, false, cancellationToken) != 5 ||
            !header.AsSpan().SequenceEqual("%PDF-"u8))
            return Problem(statusCode: 400, detail: "Invalid PDF header.");

        stream.Position = 0;
        var contract = await contracts.UploadAsync(stream, cancellationToken);
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
