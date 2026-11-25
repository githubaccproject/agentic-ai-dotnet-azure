using ContractIQ.Domain;

namespace ContractIQ.Api.Contracts;

public sealed record ContractResponse(Guid Id, string Status)
{
    public static ContractResponse From(Contract contract)
        => new(contract.Id, contract.Status.ToString());
}

public sealed record FindingResponse(string Category, string Severity, string Description, string EvidenceId);

public sealed record ReviewResponse(Guid Id, Guid ContractId, string Status, FindingResponse[] Findings)
{
    public static ReviewResponse From(Review review)
        => new(review.Id, review.ContractId, review.Status,
            review.Findings.Select(f => new FindingResponse(f.Category, f.Severity, f.Description, f.EvidenceId)).ToArray());
}
