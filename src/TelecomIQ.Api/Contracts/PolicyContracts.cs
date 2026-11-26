using System.ComponentModel.DataAnnotations;
using TelecomIQ.Domain;

namespace TelecomIQ.Api.Contracts;

public sealed class UploadPolicyRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;
}

public sealed record PolicyVersionResponse(Guid PolicyId, int Version, string Status)
{
    public static PolicyVersionResponse From(PolicyVersion value) =>
        new(value.PolicyId, value.Version, value.Status.ToString());
}
