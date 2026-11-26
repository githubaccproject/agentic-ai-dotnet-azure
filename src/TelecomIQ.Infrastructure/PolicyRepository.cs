using MongoDB.Driver;
using TelecomIQ.Application;
using TelecomIQ.Domain;

namespace TelecomIQ.Infrastructure;

public sealed class PolicyRepository(IMongoDatabase db) : IPolicyRepository
{
    private readonly IMongoCollection<PolicyVersion> policies = db.GetCollection<PolicyVersion>("policyVersions");

    public async Task<PolicyVersion> Reserve(PolicyVersion policy, CancellationToken ct)
    {
        try
        {
            await policies.InsertOneAsync(policy, cancellationToken: ct);
            return policy;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return await Get(policy.Id, ct) ?? throw new InvalidOperationException("Policy version reservation disappeared", ex);
        }
    }

    public async Task<PolicyVersion?> Get(string id, CancellationToken ct) =>
        await policies.Find(policy => policy.Id == id).FirstOrDefaultAsync(ct);

    public async Task SetStatus(string id, PolicyIngestionStatus status, CancellationToken ct)
    {
        var filters = Builders<PolicyVersion>.Filter;
        var filter = filters.Eq(policy => policy.Id, id) & filters.Ne(policy => policy.Status, PolicyIngestionStatus.Ready);
        if (status == PolicyIngestionStatus.Pending)
            filter &= filters.Eq(policy => policy.Status, PolicyIngestionStatus.Failed);

        await policies.UpdateOneAsync(filter, Builders<PolicyVersion>.Update.Set(policy => policy.Status, status),
            cancellationToken: ct);
    }
}
