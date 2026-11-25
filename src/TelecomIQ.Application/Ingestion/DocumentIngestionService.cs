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
   foreach(var page in pages) {
    for(int offset=0; offset<page.Text.Length; offset+=1400) {
     var text=page.Text.Substring(offset, Math.Min(1600,page.Text.Length-offset));
     if(string.IsNullOrWhiteSpace(text)) continue;
     await search.Index(new Chunk($"{id:N}-{page.Number}-{offset}",id,page.Number,text,await ai.Embed(text,ct)),ct);
     count++;
    }
   }
   if(count==0) throw new InvalidOperationException("No extractable text");
   await repository.Save(doc with { Status = ProcessingStatus.Ready },ct);
  } catch { if(!ct.IsCancellationRequested) await repository.Save(doc with { Status = ProcessingStatus.Failed },ct); throw; }
 }
}
