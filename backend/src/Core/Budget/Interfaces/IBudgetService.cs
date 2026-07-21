using Construcheck.Core.Budget.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Budget.Interfaces;

public interface IBudgetService
{
    Task<Result<BudgetItemResponse>> CreateAsync(Guid projectId, CreateBudgetItemRequest request, CancellationToken ct = default);
    Task<Result<BudgetSummaryResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<BudgetItemResponse>> UpdateAsync(Guid id, UpdateBudgetItemRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}