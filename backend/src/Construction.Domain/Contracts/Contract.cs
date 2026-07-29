using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Contracts;

public class Contract
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public ContractType Type { get; private set; }
    public string CounterpartyName { get; private set; } = string.Empty;
    public Money Value { get; private set; } = null!;
    public DateRange Term { get; private set; } = null!;
    public string Responsible { get; private set; } = string.Empty;
    public ContractStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Contract() { }

    public static Result<Contract> Create(
        Guid projectId, ContractType type, string counterpartyName, decimal value,
        DateOnly startDate, DateOnly dueDate, string responsible)
    {
        var valueResult = Money.CreatePositive(value);
        if (valueResult.IsFailure)
            return Result<Contract>.Validation(valueResult.Error);

        var termResult = DateRange.Create(startDate, dueDate);
        if (termResult.IsFailure)
            return Result<Contract>.Validation(termResult.Error);

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = type,
            CounterpartyName = counterpartyName,
            Value = valueResult.Value!,
            Term = termResult.Value!,
            Responsible = responsible,
            Status = ContractStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<Contract>.Success(contract);
    }

    public static Contract Reconstitute(
        Guid id, Guid projectId, ContractType type, string counterpartyName, decimal value,
        DateOnly startDate, DateOnly dueDate, string responsible, ContractStatus status,
        DateTime createdAt, DateTime updatedAt) => new()
    {
        Id = id,
        ProjectId = projectId,
        Type = type,
        CounterpartyName = counterpartyName,
        Value = Money.FromExistingValue(value),
        Term = DateRange.FromExistingValues(startDate, dueDate),
        Responsible = responsible,
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt
    };

    public Result<bool> UpdateDetails(
        ContractType type, string counterpartyName, decimal value,
        DateOnly startDate, DateOnly dueDate, string responsible)
    {
        var valueResult = Money.CreatePositive(value);
        if (valueResult.IsFailure)
            return Result<bool>.Validation(valueResult.Error);

        var termResult = DateRange.Create(startDate, dueDate);
        if (termResult.IsFailure)
            return Result<bool>.Validation(termResult.Error);

        Type = type;
        CounterpartyName = counterpartyName;
        Value = valueResult.Value!;
        Term = termResult.Value!;
        Responsible = responsible;
        UpdatedAt = DateTime.UtcNow;

        return Result<bool>.Success(true);
    }

    public void Remove()
    {
        Status = ContractStatus.Removed;
        UpdatedAt = DateTime.UtcNow;
    }
}
