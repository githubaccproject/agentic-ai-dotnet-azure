using MongoDB.Driver;
using TelecomIQ.Domain;

namespace TelecomIQ.Infrastructure;

public sealed class ChatStore(IMongoDatabase db)
{
    private IMongoCollection<ChatSession> Sessions => db.GetCollection<ChatSession>("chatSessions");
    public Task Create(ChatSession session, CancellationToken ct) => Sessions.InsertOneAsync(session, cancellationToken: ct);
    public Task<ChatSession?> Get(Guid id, string owner, CancellationToken ct) =>
        Sessions.Find(s => s.Id == id && s.Owner == owner).FirstOrDefaultAsync(ct);
    public Task<List<ChatSession>> List(string owner, Guid contract, Guid? review, CancellationToken ct) =>
        Sessions.Find(s => s.Owner == owner && s.ContractId == contract && s.ReviewId == review)
            .SortByDescending(s => s.UpdatedAtUtc).Limit(20).ToListAsync(ct);
    public async Task<bool> Append(ChatSession session, ChatTurn turn, string summary, CancellationToken ct)
    {
        var filter = Builders<ChatSession>.Filter.Where(s => s.Id == session.Id && s.Owner == session.Owner && s.Version == session.Version);
        var update = Builders<ChatSession>.Update.Push(s => s.Turns, turn)
            .Set(s => s.Summary, summary).Set(s => s.UpdatedAtUtc, turn.CreatedAtUtc).Inc(s => s.Version, 1);
        return (await Sessions.UpdateOneAsync(filter, update, cancellationToken: ct)).ModifiedCount == 1;
    }
    public async Task<bool> Delete(Guid id, string owner, CancellationToken ct) =>
        (await Sessions.DeleteOneAsync(s => s.Id == id && s.Owner == owner, ct)).DeletedCount == 1;
}
