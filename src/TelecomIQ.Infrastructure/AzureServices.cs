using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;
using TelecomIQ.Application;
using TelecomIQ.Domain;
using MongoDB.Driver;
using System.Text.Json;
namespace TelecomIQ.Infrastructure;
public sealed class BlobStorage(BlobContainerClient container):IStorage {
 public async Task Upload(string path,Stream stream,CancellationToken ct) => await container.GetBlobClient(path).UploadAsync(stream,overwrite:false,cancellationToken:ct);
 public async Task<Stream> Download(string path,CancellationToken ct) => (await container.GetBlobClient(path).DownloadStreamingAsync(cancellationToken:ct)).Value.Content;
}
public sealed class Queue(ServiceBusSender sender):IQueue {
 public async Task Send(Job job,CancellationToken ct) => await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(job)){MessageId=$"{job.Kind}-{job.ContractId}-{job.ReviewId}"},ct);
}
public sealed class Repository(IMongoDatabase db):IRepository {
 private IMongoCollection<Contract> Contracts=>db.GetCollection<Contract>("contracts");
 private IMongoCollection<Review> Reviews=>db.GetCollection<Review>("reviews");
 public async Task Save(Contract c,CancellationToken ct)=>await Contracts.ReplaceOneAsync(x=>x.Id==c.Id,c,new ReplaceOptions{IsUpsert=true},ct);
 public async Task<Contract?> Get(Guid id,CancellationToken ct)=>await Contracts.Find(x=>x.Id==id).FirstOrDefaultAsync(ct);
 public async Task SaveReview(Review r,CancellationToken ct)=>await Reviews.ReplaceOneAsync(x=>x.Id==r.Id,r,new ReplaceOptions{IsUpsert=true},ct);
 public async Task<Review?> GetReview(Guid id,CancellationToken ct)=>await Reviews.Find(x=>x.Id==id).FirstOrDefaultAsync(ct);
}
