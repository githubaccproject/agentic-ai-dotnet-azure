namespace ContractIQ.Domain;

public enum ProcessingStatus { Uploaded, Processing, Ready, Failed }
public sealed record Contract(Guid Id, string BlobPath, ProcessingStatus Status);
