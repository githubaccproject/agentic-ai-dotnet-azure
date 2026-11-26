using TelecomIQ.Domain;

namespace TelecomIQ.Api.Contracts;

public sealed record ContractResponse(Guid Id, string Status, Guid? PreviousContractId)
{
    public static ContractResponse From(Contract contract)
        => new(contract.Id, contract.Status.ToString(), contract.PreviousContractId);
}

public sealed record ReviewResponse(
    Guid Id, Guid ContractId, string Status, Finding[] Findings, PolicyReference[] Policies,
    Guid? PreviousReviewId, RevisionCheck[] RevisionChecks, FindingDecision[] Decisions)
{
    public static ReviewResponse From(Review review)
        => new(review.Id, review.ContractId, review.Status, review.Findings, review.Policies,
            review.PreviousReviewId, review.RevisionChecks, review.Decisions);
}
