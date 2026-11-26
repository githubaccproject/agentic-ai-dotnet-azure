namespace TelecomIQ.Domain;

public sealed record PolicyReference(Guid PolicyId, int Version);

public sealed record PolicyEvidence(string Id, Guid PolicyId, int Version, int Page, string Text);

public sealed record Finding(string Category, string Severity, string Description, string EvidenceId)
{
    public Guid? Id { get; init; }
    public Evidence? ContractEvidence { get; init; }
    public PolicyEvidence? PolicyEvidence { get; init; }
    public string? SuggestedWording { get; init; }
}

public sealed record RevisionCheck(
    Guid PreviousFindingId, string Category, string PreviousDescription, PolicyEvidence PolicyEvidence,
    string Status, string Explanation, Evidence? RevisedEvidence);

public sealed record FindingDecision(
    Guid Id, Guid FindingId, string Action, string Reviewer, string Reason, DateTime RecordedAtUtc)
{
    public string? ProposedWording { get; init; }
}

public sealed record ToolActivity(string Category, string Tool, long DurationMs, string Status);

public sealed record Review(Guid Id, Guid ContractId, string Status, Finding[] Findings)
{
    // Older MongoDB rows predate these collections and deserialize their fields as null.
    public PolicyReference[] Policies { get => field ?? []; init; }
    public Guid? PreviousReviewId { get; init; }
    public const string Throttled = "Throttled";
    public string? FailureCode { get; init; }
    public RevisionCheck[] RevisionChecks { get => field ?? []; init; }
    public FindingDecision[] Decisions { get => field ?? []; init; }
    public ToolActivity[] ToolActivity { get => field ?? []; init; }
}
