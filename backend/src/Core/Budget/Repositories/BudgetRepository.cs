using Construcheck.Core.Data;
using Construcheck.Core.Budget.Entities;
using Construcheck.Core.Budget.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Core.Budget.Repositories;

public class BudgetRepository(ICoreDbContext db) : IBudgetRepository
{
    public Task<BudgetItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.BudgetItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<List<BudgetItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.BudgetItems.Where(i => i.ProjectId == projectId).OrderBy(i => i.CostCenter).ToListAsync(ct);

    public async Task AddAsync(BudgetItem item, CancellationToken ct = default) =>
        await db.BudgetItems.AddAsync(item, ct);

    public async Task AddRangeAsync(IEnumerable<BudgetItem> items, CancellationToken ct = default) =>
        await db.BudgetItems.AddRangeAsync(items, ct);

    public Task DeleteAsync(BudgetItem item, CancellationToken ct = default)
    {
        db.BudgetItems.Remove(item);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}