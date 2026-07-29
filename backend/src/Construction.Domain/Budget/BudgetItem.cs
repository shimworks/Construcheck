using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Budget;

public class BudgetItem
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string CostCenter { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public string? SinapiCode { get; private set; }
    public BudgetItemStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Money TotalValue => UnitPrice * Quantity;

    private BudgetItem() { }

    public static Result<BudgetItem> Create(
        Guid projectId, string costCenter, string description, string unit,
        decimal quantity, decimal unitPrice, string? sinapiCode)
    {
        if (quantity <= 0)
            return Result<BudgetItem>.Validation("A quantidade deve ser maior que zero.");

        var unitPriceResult = Money.CreateNonNegative(unitPrice);
        if (unitPriceResult.IsFailure)
            return Result<BudgetItem>.Validation(unitPriceResult.Error);

        var item = new BudgetItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CostCenter = costCenter,
            Description = description,
            Unit = unit,
            Quantity = quantity,
            UnitPrice = unitPriceResult.Value!,
            SinapiCode = sinapiCode,
            Status = BudgetItemStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<BudgetItem>.Success(item);
    }

    public static BudgetItem Reconstitute(
        Guid id, Guid projectId, string costCenter, string description, string unit,
        decimal quantity, decimal unitPrice, string? sinapiCode, BudgetItemStatus status,
        DateTime createdAt, DateTime updatedAt) => new()
    {
        Id = id,
        ProjectId = projectId,
        CostCenter = costCenter,
        Description = description,
        Unit = unit,
        Quantity = quantity,
        UnitPrice = Money.FromExistingValue(unitPrice),
        SinapiCode = sinapiCode,
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt
    };

    public Result<bool> UpdateDetails(
        string costCenter, string description, string unit,
        decimal quantity, decimal unitPrice, string? sinapiCode)
    {
        if (quantity <= 0)
            return Result<bool>.Validation("A quantidade deve ser maior que zero.");

        var unitPriceResult = Money.CreateNonNegative(unitPrice);
        if (unitPriceResult.IsFailure)
            return Result<bool>.Validation(unitPriceResult.Error);

        CostCenter = costCenter;
        Description = description;
        Unit = unit;
        Quantity = quantity;
        UnitPrice = unitPriceResult.Value!;
        SinapiCode = sinapiCode;
        UpdatedAt = DateTime.UtcNow;

        return Result<bool>.Success(true);
    }

    public void Remove()
    {
        Status = BudgetItemStatus.Removed;
        UpdatedAt = DateTime.UtcNow;
    }
}
