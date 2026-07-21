using Construcheck.Core.Contracts.Enums;

namespace Construcheck.Core.Contracts.Entities;

public class Contract
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ContractType Type { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Responsible { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}