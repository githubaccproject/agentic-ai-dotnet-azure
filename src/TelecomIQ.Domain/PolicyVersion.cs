using System.Globalization;

namespace TelecomIQ.Domain;

public enum PolicyIngestionStatus { Pending, Processing, Ready, Failed }

public sealed record PolicyVersion(
    string Id, Guid PolicyId, int Version, string BlobPath, string ContentHash, PolicyIngestionStatus Status)
{
    public static string Key(Guid policyId, int version) =>
        $"{policyId:N}-{version.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record PolicyChunk(string Id, Guid PolicyId, int Version, int Page, string Text, float[] Vector);
