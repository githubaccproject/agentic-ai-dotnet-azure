using System.Security.Cryptography;
using TelecomIQ.Domain;

namespace TelecomIQ.Application.Services;

public sealed class PolicyUploadService(IPolicyRepository repository, IPolicyStorage storage, IQueue queue)
{
    public async Task<PolicyVersion?> Upload(Guid policyId, int version, Stream stream, CancellationToken ct)
    {
        var key = PolicyVersion.Key(policyId, version);
        var position = stream.Position;
        var contentHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        stream.Position = position;

        var policy = await repository.Reserve(
            new PolicyVersion(key, policyId, version, $"{key}/{contentHash}/policy.pdf", contentHash,
                PolicyIngestionStatus.Pending), ct);

        if (policy.ContentHash != contentHash)
            return null;

        if (policy.Status is PolicyIngestionStatus.Ready or PolicyIngestionStatus.Processing)
            return policy;

        await storage.EnsureUploaded(policy.BlobPath, stream, ct);
        if (policy.Status == PolicyIngestionStatus.Failed)
            await repository.SetStatus(policy.Id, PolicyIngestionStatus.Pending, ct);

        await queue.Send(new Job("ingest-policy", Guid.Empty, PolicyVersionId: policy.Id), ct);
        return await repository.Get(policy.Id, ct) ?? policy;
    }

    public Task<PolicyVersion?> Get(Guid policyId, int version, CancellationToken ct) =>
        repository.Get(PolicyVersion.Key(policyId, version), ct);
}
