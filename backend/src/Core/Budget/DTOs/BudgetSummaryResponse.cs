namespace Construcheck.Core.Budget.DTOs;

public record BudgetSummaryResponse(
    List<BudgetItemResponse> Items,
    List<TotalByCostCenterResponse> TotalsByCostCenter,
    decimal ProjectTotalValue);
