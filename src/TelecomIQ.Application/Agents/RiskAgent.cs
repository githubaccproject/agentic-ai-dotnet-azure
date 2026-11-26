using TelecomIQ.Application.RAG;
using TelecomIQ.Domain;

namespace TelecomIQ.Application.Agents;

public sealed class RiskAgent(RagService rag, IAi ai)
{
    private static readonly string[] Severities = ["Low", "Medium", "High", "Critical"];

    public async Task<Finding[]> Run(Guid id, string category, string query, CancellationToken ct)
    {
        var evidence = await rag.Retrieve(id, query, ct);
        if (evidence.Length == 0) return [];

        var findings = await ai.Analyze(category, evidence, ct);
        var passages = evidence.ToDictionary(e => e.Id);
        return findings
            .Where(f => f is not null && f.EvidenceId is not null && passages.ContainsKey(f.EvidenceId)
                && Severities.Contains(f.Severity))
            .Select(f => new Finding(category, f.Severity, f.Description, f.EvidenceId)
            {
                Id = Guid.NewGuid(),
                ContractEvidence = passages[f.EvidenceId]
            })
            .ToArray();
    }
}
