using Construcheck.Construction.Domain.Contracts;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Construction.Infrastructure.Repositories;

public class ContractRepository(ConstructionDbContext db) : IContractRepository
{
    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Contracts.FirstOrDefaultAsync(c => c.Id == id && c.Status == ContractStatus.Active, ct);

    public Task<List<Contract>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.Contracts
          .Where(c => c.ProjectId == projectId && c.Status == ContractStatus.Active)
          .OrderBy(c => c.Term.End)
          .ToListAsync(ct);

    public async Task AddAsync(Contract contract, CancellationToken ct = default) =>
        await db.Contracts.AddAsync(contract, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
