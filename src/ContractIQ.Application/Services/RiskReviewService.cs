using ContractIQ.Domain;

namespace ContractIQ.Application.Services;

public enum ReviewSubmissionStatus { Accepted, ContractNotFound, ContractNotReady }
public sealed record ReviewSubmission(ReviewSubmissionStatus Status, Review? Review = null);

public interface IRiskReviewService
{
    Task<ReviewSubmission> SubmitAsync(Guid contractId, CancellationToken cancellationToken);
    Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class RiskReviewService(IRepository repository, IQueue queue) : IRiskReviewService
{
    public async Task<ReviewSubmission> SubmitAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await repository.Get(contractId, cancellationToken);
        if (contract is null)
            return new(ReviewSubmissionStatus.ContractNotFound);
        if (contract.Status != ProcessingStatus.Ready)
            return new(ReviewSubmissionStatus.ContractNotReady);

        var review = new Review(Guid.NewGuid(), contractId, "Queued", []);
        await repository.SaveReview(review, cancellationToken);
        await queue.Send(new Job("review", contractId, review.Id), cancellationToken);
        return new(ReviewSubmissionStatus.Accepted, review);
    }

    public Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken)
        => repository.GetReview(id, cancellationToken);
}
