using TelecomIQ.Domain;

namespace TelecomIQ.Application.Ingestion;

public sealed class PolicyIngestionService(
    IPolicyRepository repository, IPolicyStorage storage, IExtractor extractor, IAi ai, IPolicySearch search)
{
    public async Task Run(string id, CancellationToken ct)
    {
        var policy = await repository.Get(id, ct) ?? throw new InvalidOperationException("Unknown policy version");
        if (policy.Status == PolicyIngestionStatus.Ready)
            return;

        await repository.SetStatus(id, PolicyIngestionStatus.Processing, ct);
        try
        {
            await using var stream = await storage.Download(policy.BlobPath, ct);
            var pages = await extractor.Extract(stream, ct);
            var chunks = DocumentChunker.Split(pages).ToArray();
            if (chunks.Length == 0)
                throw new InvalidOperationException("No extractable policy text");

            foreach (var (page, offset, text) in chunks)
            {
                var vector = await ai.Embed(text, ct);
                await search.Index(new PolicyChunk($"{policy.Id}-{page}-{offset}", policy.PolicyId, policy.Version,
                    page, text, vector), ct);
            }

            await repository.SetStatus(id, PolicyIngestionStatus.Ready, ct);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await repository.SetStatus(id, PolicyIngestionStatus.Failed, ct);
            throw;
        }
    }
}
