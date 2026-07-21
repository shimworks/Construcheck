using Construcheck.Core.Budget.Entities;

namespace Construcheck.Core.Budget.Interfaces;

public interface IBudgetRepository
{
    Task<BudgetItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<BudgetItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(BudgetItem item, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<BudgetItem> itens, CancellationToken ct = default);
    Task DeleteAsync(BudgetItem item, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}