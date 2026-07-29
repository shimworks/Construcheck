namespace Construcheck.Construction.Application.Budget.DTOs;

public record UpdateBudgetItemRequest(
    string CostCenter, string Description, string Unit,
    decimal Quantity, decimal UnitPrice, string? SinapiCode);
