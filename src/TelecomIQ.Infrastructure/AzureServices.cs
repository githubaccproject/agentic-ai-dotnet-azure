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
 public async Task Send(Job job,CancellationToken ct) => await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(job)){
  MessageId = job.PolicyVersionId is string id ? $"{job.Kind}-{id}" : $"{job.Kind}-{job.ContractId}-{job.ReviewId}"
 },ct);
}
public sealed class Repository(IMongoDatabase db):IRepository {
 private IMongoCollection<Contract> Contracts=>db.GetCollection<Contract>("contracts");
 private IMongoCollection<Review> Reviews=>db.GetCollection<Review>("reviews");
 public async Task Save(Contract c,CancellationToken ct)=>await Contracts.ReplaceOneAsync(x=>x.Id==c.Id,c,new ReplaceOptions{IsUpsert=true},ct);
 public async Task<Contract?> Get(Guid id,CancellationToken ct)=>await Contracts.Find(x=>x.Id==id).FirstOrDefaultAsync(ct);
 public async Task SaveReview(Review r,CancellationToken ct)=>await Reviews.ReplaceOneAsync(x=>x.Id==r.Id,r,new ReplaceOptions{IsUpsert=true},ct);
 public async Task<bool> UpdateReviewProgress(Review review, CancellationToken ct)
 {
  var filter = Builders<Review>.Filter.Where(r => r.Id == review.Id && r.Status != "Completed");
  var update = Builders<Review>.Update.Set(r => r.Status, review.Status)
   .Set(r => r.FailureCode, review.FailureCode);
  if (review.Status == "Completed")
   update = update.Set(r => r.Findings, review.Findings).Set(r => r.RevisionChecks, review.RevisionChecks).Set(r => r.ToolActivity, review.ToolActivity);
  return (await Reviews.UpdateOneAsync(filter, update, cancellationToken: ct)).MatchedCount == 1;
 }

 public async Task<bool> AppendDecision(Guid reviewId, FindingDecision decision, CancellationToken ct)
 {
  var filters = Builders<Review>.Filter;
  var filter = filters.Where(r => r.Id == reviewId && r.Status == "Completed")
   & filters.ElemMatch(r => r.Findings, finding => finding.Id == decision.FindingId)
   & filters.Not(filters.ElemMatch(r => r.Decisions, item => item.Id == decision.Id));
  return (await Reviews.UpdateOneAsync(filter, Builders<Review>.Update.Push(r => r.Decisions, decision),
   cancellationToken: ct)).MatchedCount == 1;
 }

 public async Task<Review?> GetReview(Guid id,CancellationToken ct)=>await Reviews.Find(x=>x.Id==id).FirstOrDefaultAsync(ct);
}
