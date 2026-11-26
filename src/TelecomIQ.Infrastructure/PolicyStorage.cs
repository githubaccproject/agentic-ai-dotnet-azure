using Azure;
using Azure.Storage.Blobs;
using TelecomIQ.Application;

namespace TelecomIQ.Infrastructure;

public sealed class PolicyStorage(BlobContainerClient container) : IPolicyStorage
{
    public async Task EnsureUploaded(string path, Stream stream, CancellationToken ct)
    {
        try
        {
            await container.GetBlobClient(path).UploadAsync(stream, overwrite: false, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode is "BlobAlreadyExists" or "ConditionNotMet")
        {
            // The repository has already matched the content hash for this immutable blob path.
        }
    }

    public async Task<Stream> Download(string path, CancellationToken ct) =>
        (await container.GetBlobClient(path).DownloadStreamingAsync(cancellationToken: ct)).Value.Content;
}
