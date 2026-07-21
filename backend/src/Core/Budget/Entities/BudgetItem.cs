namespace Construcheck.Core.Budget.Entities;

public class BudgetItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string CostCenter { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? SinapiCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public decimal TotalValue => Quantity * UnitPrice;
}