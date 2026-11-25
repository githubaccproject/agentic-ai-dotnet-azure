using ContractIQ.Domain;
namespace ContractIQ.Application.RAG;
public sealed class RagService(IAi ai, ISearch search) {
 public async Task<Evidence[]> Retrieve(Guid id,string query,CancellationToken ct) =>
  await search.Retrieve(id,query,await ai.Embed(query,ct),ct);
}
