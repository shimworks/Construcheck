using Construcheck.Construction.Domain.Budget;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Construction.Infrastructure.Repositories;

public class BudgetItemRepository(ConstructionDbContext db) : IBudgetItemRepository
{
    public Task<BudgetItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.BudgetItems.FirstOrDefaultAsync(i => i.Id == id && i.Status == BudgetItemStatus.Active, ct);

    public Task<List<BudgetItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.BudgetItems
          .Where(i => i.ProjectId == projectId && i.Status == BudgetItemStatus.Active)
          .OrderBy(i => i.CostCenter)
          .ToListAsync(ct);

    public async Task AddAsync(BudgetItem item, CancellationToken ct = default) =>
        await db.BudgetItems.AddAsync(item, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
