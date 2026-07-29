using Construcheck.Construction.Application.Budget.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Budget.Interfaces;

public interface IBudgetApplicationService
{
    Task<Result<BudgetItemResponse>> CreateAsync(Guid projectId, CreateBudgetItemRequest request, CancellationToken ct = default);
    Task<Result<BudgetSummaryResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<BudgetItemResponse>> UpdateAsync(Guid id, UpdateBudgetItemRequest request, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default);
}
