using Construcheck.Core.Contracts.Enums;

namespace Construcheck.Core.Contracts.DTOs;

public record ContractResponse(
    Guid Id, Guid ProjectId, ContractType Type, string CounterpartyName, decimal Value,
    DateOnly StartDate, DateOnly DueDate, string Responsible);
