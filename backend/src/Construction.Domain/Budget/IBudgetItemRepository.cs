namespace Construcheck.Construction.Domain.Budget;

public interface IBudgetItemRepository
{
    Task<BudgetItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<BudgetItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(BudgetItem item, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
