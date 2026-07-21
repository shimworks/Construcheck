namespace Construcheck.Core.Budget.DTOs;

public record CreateBudgetItemRequest(
    string CostCenter, string Description, string Unit,
    decimal Quantity, decimal UnitPrice, string? SinapiCode);