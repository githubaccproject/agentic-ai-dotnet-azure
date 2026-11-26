using TelecomIQ.Domain;

namespace TelecomIQ.Application.Services;

public interface IContractService
{
    Task<Contract?> UploadAsync(Stream stream, Guid? previousContractId, CancellationToken cancellationToken);
    Task<Contract?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ContractService(IStorage storage, IRepository repository, IQueue queue)
    : IContractService
{
    public async Task<Contract?> UploadAsync(Stream stream, Guid? previousContractId, CancellationToken cancellationToken)
    {
        if (previousContractId is Guid previousId && await repository.Get(previousId, cancellationToken) is null)
            return null;

        var id = Guid.NewGuid();
        var path = $"{id:N}/contract.pdf";
        var contract = new Contract(id, path, ProcessingStatus.Uploaded) { PreviousContractId = previousContractId };

        await storage.Upload(path, stream, cancellationToken);
        await repository.Save(contract, cancellationToken);
        await queue.Send(new Job("ingest", id), cancellationToken);
        return contract;
    }

    public Task<Contract?> GetAsync(Guid id, CancellationToken cancellationToken)
        => repository.Get(id, cancellationToken);
}
