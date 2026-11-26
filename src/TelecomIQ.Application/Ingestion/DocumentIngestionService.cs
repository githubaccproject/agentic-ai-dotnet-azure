using TelecomIQ.Domain;
namespace TelecomIQ.Application.Ingestion;
public sealed class DocumentIngestionService(IRepository repository, IStorage storage, IExtractor extractor, IAi ai, ISearch search)
{
 public async Task Run(Guid id, CancellationToken ct) {
  var doc = await repository.Get(id, ct) ?? throw new InvalidOperationException("Unknown contract");
  if (doc.Status == ProcessingStatus.Ready) return;
  await repository.Save(doc with { Status = ProcessingStatus.Processing }, ct);
  try {
   await using var stream = await storage.Download(doc.BlobPath, ct);
   var pages = await extractor.Extract(stream, ct);
   var count = 0;
   foreach (var (page, offset, text) in DocumentChunker.Split(pages)) {
    await search.Index(new Chunk($"{id:N}-{page}-{offset}", id, page, text, await ai.Embed(text, ct)), ct);
    count++;
   }
   if(count==0) throw new InvalidOperationException("No extractable text");
   await repository.Save(doc with { Status = ProcessingStatus.Ready },ct);
  } catch { if(!ct.IsCancellationRequested) await repository.Save(doc with { Status = ProcessingStatus.Failed },ct); throw; }
 }
}
