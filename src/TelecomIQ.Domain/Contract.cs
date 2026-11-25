namespace TelecomIQ.Domain;
public enum ProcessingStatus { Uploaded, Processing, Ready, Failed }
public sealed record Contract(Guid Id, string BlobPath, ProcessingStatus Status);
public sealed record Chunk(string Id, Guid ContractId, int Page, string Text, float[] Vector);
public sealed record Evidence(string Id, int Page, string Text);
public sealed record Finding(string Category, string Severity, string Description, string EvidenceId);
public sealed record Review(Guid Id, Guid ContractId, string Status, Finding[] Findings);
public sealed record Job(string Kind, Guid ContractId, Guid? ReviewId = null);
public sealed record Page(int Number, string Text);
