using Construcheck.Core.Contracts.Entities;
using Construcheck.Core.Contracts.Interfaces;
using Construcheck.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace Construcheck.Core.Contracts.Repositories;

public class ContractRepository(ICoreDbContext db) : IContractRepository
{
    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<Contract>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.Contracts.Where(c => c.ProjectId == projectId).OrderBy(c => c.DueDate).ToListAsync(ct);

    public async Task AddAsync(Contract contract, CancellationToken ct = default) =>
        await db.Contracts.AddAsync(contract, ct);

    public Task DeleteAsync(Contract contract, CancellationToken ct = default)
    {
        db.Contracts.Remove(contract);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}