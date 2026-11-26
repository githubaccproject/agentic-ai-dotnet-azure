using TelecomIQ.Domain;

namespace TelecomIQ.Application.RAG;

public sealed class RagService(IAi ai, ISearch search, IPolicySearch policies)
{
    public async Task<Evidence[]> Retrieve(Guid id, string query, CancellationToken ct) =>
        await search.Retrieve(id, query, await ai.Embed(query, ct), ct);

    public async Task<(Evidence[] Contract, PolicyEvidence[] Policy)> RetrieveComparison(
        Guid id, string query, PolicyReference[] selected, CancellationToken ct)
    {
        var vector = await ai.Embed(query, ct);
        var contractTask = search.Retrieve(id, query, vector, ct);
        var policyTask = policies.Retrieve(selected, query, vector, ct);
        await Task.WhenAll(contractTask, policyTask);
        return (await contractTask, await policyTask);
    }
}
