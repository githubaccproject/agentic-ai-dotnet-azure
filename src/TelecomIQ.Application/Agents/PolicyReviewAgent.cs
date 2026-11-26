using TelecomIQ.Application.RAG;
using TelecomIQ.Domain;

namespace TelecomIQ.Application.Agents;

public sealed class PolicyReviewAgent(RagService rag, IPolicyReviewAi ai)
{
    private static readonly string[] Severities = ["Low", "Medium", "High", "Critical"];
    private const int MaxFindingsPerCategory = 4;
    private const int MaxFindingTextLength = 1500;

    public async Task<Finding[]> Run(Guid contractId, string category, string query,
        PolicyReference[] selected, CancellationToken ct)
    {
        var (contractEvidence, policyEvidence) = await rag.RetrieveComparison(contractId, query, selected, ct);
        var contracts = contractEvidence.ToDictionary(e => e.Id);
        var policies = policyEvidence
            .Where(e => selected.Contains(new PolicyReference(e.PolicyId, e.Version)))
            .ToDictionary(e => e.Id);

        if (contracts.Count == 0 || policies.Count == 0)
            throw new InvalidOperationException("Contract or selected policy evidence could not be retrieved.");

        var findings = await ai.Compare(category, contractEvidence, policies.Values.ToArray(), ct);
        var grounded = new List<Finding>();
        foreach (var candidate in findings)
        {
            if (candidate is null || !Severities.Contains(candidate.Severity)
                || !ValidText(candidate.Description) || string.IsNullOrEmpty(candidate.ContractEvidenceId)
                || !contracts.TryGetValue(candidate.ContractEvidenceId, out var contract)
                || string.IsNullOrEmpty(candidate.PolicyEvidenceId)
                || !policies.TryGetValue(candidate.PolicyEvidenceId, out var policy))
                continue;

            grounded.Add(new Finding(category, candidate.Severity, candidate.Description, contract.Id)
            {
                Id = Guid.NewGuid(),
                ContractEvidence = contract,
                PolicyEvidence = policy,
                SuggestedWording = ValidText(candidate.SuggestedWording) ? candidate.SuggestedWording : null
            });

            if (grounded.Count == MaxFindingsPerCategory)
                break;
        }

        return grounded.ToArray();
    }

    public async Task<RevisionCheck?> Recheck(Guid contractId, Finding previous, CancellationToken ct)
    {
        if (previous.Id is not Guid findingId || previous.PolicyEvidence is not { } policy)
            return null;

        var evidence = await rag.Retrieve(contractId, previous.Description, ct);
        var analysis = evidence.Length == 0 ? null : await ai.Recheck(previous, evidence, ct);
        var cited = evidence.FirstOrDefault(e => e.Id == analysis?.ContractEvidenceId);
        if (analysis is null || cited is null || !ValidText(analysis.Explanation)
            || analysis.Status is not ("Resolved" or "StillPresent"))
            return new(findingId, previous.Category, previous.Description, policy, "NeedsReview",
                "The revised agreement evidence did not establish whether this issue was resolved.", null);

        return new(findingId, previous.Category, previous.Description, policy, analysis.Status, analysis.Explanation, cited);
    }

    private static bool ValidText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxFindingTextLength;
}
