namespace Construcheck.Construction.Application.Budget.DTOs;

public record BudgetSummaryResponse(
    List<BudgetItemResponse> Items,
    List<TotalByCostCenterResponse> TotalsByCostCenter,
    decimal ProjectTotalValue);
