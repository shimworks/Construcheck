namespace Construcheck.Construction.Domain.Contracts;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Contract>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Contract contract, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
