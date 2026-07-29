namespace Construcheck.Construction.Application.Budget.DTOs;

public record BudgetItemResponse(
    Guid Id, Guid ProjectId, string CostCenter, string Description, string Unit,
    decimal Quantity, decimal UnitPrice, decimal TotalValue, string? SinapiCode);
