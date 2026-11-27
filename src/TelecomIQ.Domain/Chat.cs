namespace TelecomIQ.Domain;

public sealed record ChatCitation(string Id, string Source, int Page, string Text, Guid? PolicyId = null, int? PolicyVersion = null);
public sealed record ChatUsage(int PromptTokens, int CompletionTokens, int EmbeddingTokens, int UnreportedCalls,
    decimal? EstimatedUsd, long DurationMs, int ModelCalls);
public sealed record ChatTurn(Guid Id, string Question, string Answer, string Route, ChatCitation[] Citations,
    ToolActivity[] Tools, ChatUsage Usage, DateTime CreatedAtUtc, string Grounding);
public sealed record ChatSession(Guid Id, string Owner, Guid ContractId, Guid? ReviewId, DateTime CreatedAtUtc)
{
    public ChatTurn[] Turns { get; init; } = [];
    public string Summary { get; init; } = "";
    public int Version { get; init; }
    public DateTime UpdatedAtUtc { get; init; } = CreatedAtUtc;
}
