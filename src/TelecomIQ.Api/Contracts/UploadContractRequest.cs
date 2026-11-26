using System.ComponentModel.DataAnnotations;

namespace TelecomIQ.Api.Contracts;

public sealed class UploadContractRequest
{
    public Guid? PreviousContractId { get; init; }

    [Required]
    public IFormFile File { get; init; } = null!;
}
