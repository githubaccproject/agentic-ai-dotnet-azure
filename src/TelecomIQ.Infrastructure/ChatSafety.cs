using TelecomIQ.Domain;

namespace TelecomIQ.Infrastructure;

public static class ChatSafety
{
    public const int MaxQuestion = 1500;
    public const int MaxTurns = 30;
    public const string Insufficient = "I could not establish an answer from the available agreement, selected policies or verified review records. Please narrow the question or select a completed policy review.";
    public static readonly string[] Routes = ["Vendor Legal", "Commercial", "Telecom Security & Compliance", "General"];
    public static bool ValidQuestion(string? question) =>
        !string.IsNullOrWhiteSpace(question) && question.Length <= MaxQuestion && !question.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    public static (string Answer, ChatCitation[] Citations, string Grounding) Validate(
        string? answer, string[]? ids, IReadOnlyDictionary<string, ChatCitation> available)
    {
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 6000 || ids is null || ids.Length == 0 ||
            ids.Length > 12 || ids.Any(id => string.IsNullOrWhiteSpace(id) || !available.ContainsKey(id)))
            return (Insufficient, [], "InsufficientEvidence");
        return (answer.Trim(), ids.Distinct().Select(id => available[id]).ToArray(), "CitationIdsValidated");
    }
}
