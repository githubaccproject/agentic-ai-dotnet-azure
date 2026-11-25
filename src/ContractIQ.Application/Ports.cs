using ContractIQ.Domain;
namespace ContractIQ.Application;
public interface IStorage { Task Upload(string path, Stream stream, CancellationToken ct); Task<Stream> Download(string path, CancellationToken ct); }
public interface IRepository {
 Task Save(Contract contract, CancellationToken ct); Task<Contract?> Get(Guid id, CancellationToken ct);
 Task SaveReview(Review review, CancellationToken ct); Task<Review?> GetReview(Guid id, CancellationToken ct);
}
public interface IQueue { Task Send(Job job, CancellationToken ct); }
public interface IExtractor { Task<Page[]> Extract(Stream stream, CancellationToken ct); }
public interface IAi { Task<float[]> Embed(string text, CancellationToken ct); Task<Finding[]> Analyze(string category, Evidence[] evidence, CancellationToken ct); }
public interface ISearch { Task Index(Chunk chunk, CancellationToken ct); Task<Evidence[]> Retrieve(Guid id, string query, float[] vector, CancellationToken ct); }
