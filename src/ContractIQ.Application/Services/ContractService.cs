using ContractIQ.Domain;

namespace ContractIQ.Application.Services;

public interface IContractService
{
    Task<Contract> UploadAsync(Stream stream, CancellationToken cancellationToken);
    Task<Contract?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ContractService(IStorage storage, IRepository repository, IQueue queue)
    : IContractService
{
    public async Task<Contract> UploadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var path = $"{id:N}/contract.pdf";
        var contract = new Contract(id, path, ProcessingStatus.Uploaded);

        await storage.Upload(path, stream, cancellationToken);
        await repository.Save(contract, cancellationToken);
        await queue.Send(new Job("ingest", id), cancellationToken);
        return contract;
    }

    public Task<Contract?> GetAsync(Guid id, CancellationToken cancellationToken)
        => repository.Get(id, cancellationToken);
}
