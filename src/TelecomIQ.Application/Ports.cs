using TelecomIQ.Domain;
namespace TelecomIQ.Application;
public interface IStorage { Task Upload(string path, Stream stream, CancellationToken ct); Task<Stream> Download(string path, CancellationToken ct); }
public interface IRepository {
 Task Save(Contract contract, CancellationToken ct); Task<Contract?> Get(Guid id, CancellationToken ct);
 Task SaveReview(Review review, CancellationToken ct); Task<Review?> GetReview(Guid id, CancellationToken ct);
 Task<bool> UpdateReviewProgress(Review review, CancellationToken ct);
 Task<bool> AppendDecision(Guid reviewId, FindingDecision decision, CancellationToken ct);
}
public interface IQueue { Task Send(Job job, CancellationToken ct); }
public interface IExtractor { Task<Page[]> Extract(Stream stream, CancellationToken ct); }
public interface IAi { Task<float[]> Embed(string text, CancellationToken ct); Task<Finding[]> Analyze(string category, Evidence[] evidence, CancellationToken ct); }
public sealed record PolicyAnalysis(string Severity, string Description, string ContractEvidenceId,
    string PolicyEvidenceId, string? SuggestedWording);
public sealed record RevisionAnalysis(string Status, string Explanation, string? ContractEvidenceId);

public interface IPolicyReviewAi
{
    Task<PolicyAnalysis[]> Compare(string category, Evidence[] contract, PolicyEvidence[] policy, CancellationToken ct);
    Task<RevisionAnalysis> Recheck(Finding previous, Evidence[] revised, CancellationToken ct);
}

public interface ISearch { Task Index(Chunk chunk, CancellationToken ct); Task<Evidence[]> Retrieve(Guid id, string query, float[] vector, CancellationToken ct); }

public interface IPolicyStorage
{
    Task EnsureUploaded(string path, Stream stream, CancellationToken ct);
    Task<Stream> Download(string path, CancellationToken ct);
}

public interface IPolicyRepository
{
    Task<PolicyVersion[]> ListReady(CancellationToken ct);
    Task<PolicyVersion> Reserve(PolicyVersion policy, CancellationToken ct);
    Task<PolicyVersion?> Get(string id, CancellationToken ct);
    Task SetStatus(string id, PolicyIngestionStatus status, CancellationToken ct);
}

public interface IPolicySearch
{
    Task Index(PolicyChunk chunk, CancellationToken ct);
    Task<PolicyEvidence[]> Retrieve(PolicyReference[] policies, string query, float[] vector, CancellationToken ct);
}
