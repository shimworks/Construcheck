using Construcheck.Construction.Application.Budget.DTOs;
using Construcheck.Construction.Application.Budget.Interfaces;
using Construcheck.Construction.Domain.Budget;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Budget.Services;

public class BudgetApplicationService(IBudgetItemRepository repository, IProjectRepository projectRepository) : IBudgetApplicationService
{
    public async Task<Result<BudgetItemResponse>> CreateAsync(Guid projectId, CreateBudgetItemRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<BudgetItemResponse>.NotFound("Obra não encontrada.");

        var itemResult = BudgetItem.Create(
            projectId, request.CostCenter, request.Description, request.Unit,
            request.Quantity, request.UnitPrice, request.SinapiCode);

        if (itemResult.IsFailure)
            return Result<BudgetItemResponse>.Validation(itemResult.Error);

        var item = itemResult.Value!;

        await repository.AddAsync(item, ct);
        await repository.SaveChangesAsync(ct);

        return Result<BudgetItemResponse>.Success(ToResponse(item));
    }

    public async Task<Result<BudgetSummaryResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        // O Repository já filtra Status == Active internamente — itens removidos não entram aqui.
        var items = await repository.GetByProjectIdAsync(projectId, ct);

        var totals = items
            .GroupBy(i => i.CostCenter)
            .Select(g => new TotalByCostCenterResponse(g.Key, g.Sum(i => i.TotalValue.Amount)))
            .OrderBy(t => t.CostCenter)
            .ToList();

        var summary = new BudgetSummaryResponse(
            items.Select(ToResponse).ToList(),
            totals,
            items.Sum(i => i.TotalValue.Amount));

        return Result<BudgetSummaryResponse>.Success(summary);
    }

    public async Task<Result<BudgetItemResponse>> UpdateAsync(Guid id, UpdateBudgetItemRequest request, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct);
        if (item is null)
            return Result<BudgetItemResponse>.NotFound("Item de orçamento não encontrado.");

        var updateResult = item.UpdateDetails(
            request.CostCenter, request.Description, request.Unit,
            request.Quantity, request.UnitPrice, request.SinapiCode);

        if (updateResult.IsFailure)
            return Result<BudgetItemResponse>.Validation(updateResult.Error);

        await repository.SaveChangesAsync(ct);

        return Result<BudgetItemResponse>.Success(ToResponse(item));
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct);
        if (item is null)
            return Result<bool>.NotFound("Item de orçamento não encontrado.");

        item.Remove();
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static BudgetItemResponse ToResponse(BudgetItem item) => new(
        item.Id, item.ProjectId, item.CostCenter, item.Description, item.Unit,
        item.Quantity, item.UnitPrice.Amount, item.TotalValue.Amount, item.SinapiCode);
}
