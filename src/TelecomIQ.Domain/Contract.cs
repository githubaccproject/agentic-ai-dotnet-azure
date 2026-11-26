namespace TelecomIQ.Domain;
public enum ProcessingStatus { Uploaded, Processing, Ready, Failed }
public sealed record Contract(Guid Id, string BlobPath, ProcessingStatus Status)
{
    public Guid? PreviousContractId { get; init; }
}
public sealed record Chunk(string Id, Guid ContractId, int Page, string Text, float[] Vector);
public sealed record Evidence(string Id, int Page, string Text);
public sealed record Job(
    string Kind, Guid ContractId, Guid? ReviewId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? PolicyVersionId = null);
public sealed record Page(int Number, string Text);
