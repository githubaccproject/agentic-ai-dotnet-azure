using System.ComponentModel.DataAnnotations;

namespace ContractIQ.Api.Contracts;

public sealed class UploadContractRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;
}
