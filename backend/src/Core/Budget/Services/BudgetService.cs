using Construcheck.Core.Projects.Interfaces;
using Construcheck.Core.Budget.DTOs;
using Construcheck.Core.Budget.Entities;
using Construcheck.Core.Budget.Interfaces;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Budget.Services;

public class BudgetService(IBudgetRepository repository, IProjectRepository projectRepository) : IBudgetService
{
    public async Task<Result<BudgetItemResponse>> CreateAsync(Guid projectId, CreateBudgetItemRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<BudgetItemResponse>.NotFound("Obra não encontrada.");

        var item = new BudgetItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CostCenter = request.CostCenter,
            Description = request.Description,
            Unit = request.Unit,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            SinapiCode = request.SinapiCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(item, ct);
        await repository.SaveChangesAsync(ct);

        return Result<BudgetItemResponse>.Success(ToResponse(item));
    }

    public async Task<Result<BudgetSummaryResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var items = await repository.GetByProjectIdAsync(projectId, ct);

        var totals = items
            .GroupBy(i => i.CostCenter)
            .Select(g => new TotalByCostCenterResponse(g.Key, g.Sum(i => i.TotalValue)))
            .OrderBy(t => t.CostCenter)
            .ToList();

        var summary = new BudgetSummaryResponse(
            items.Select(ToResponse).ToList(),
            totals,
            items.Sum(i => i.TotalValue));

        return Result<BudgetSummaryResponse>.Success(summary);
    }

    public async Task<Result<BudgetItemResponse>> UpdateAsync(Guid id, UpdateBudgetItemRequest request, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct);
        if (item is null)
            return Result<BudgetItemResponse>.NotFound("Item de orçamento não encontrado.");

        item.CostCenter = request.CostCenter;
        item.Description = request.Description;
        item.Unit = request.Unit;
        item.Quantity = request.Quantity;
        item.UnitPrice = request.UnitPrice;
        item.SinapiCode = request.SinapiCode;
        item.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(ct);

        return Result<BudgetItemResponse>.Success(ToResponse(item));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct);
        if (item is null)
            return Result<bool>.NotFound("Item de orçamento não encontrado.");

        await repository.DeleteAsync(item, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    internal static BudgetItemResponse ToResponse(BudgetItem item) => new(
        item.Id, item.ProjectId, item.CostCenter, item.Description, item.Unit,
        item.Quantity, item.UnitPrice, item.TotalValue, item.SinapiCode);
}