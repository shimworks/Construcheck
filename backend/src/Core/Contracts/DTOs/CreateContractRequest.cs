using Construcheck.Core.Contracts.Enums;

namespace Construcheck.Core.Contracts.DTOs;

public record CreateContractRequest(
    ContractType Type, string CounterpartyName, decimal Value,
    DateOnly StartDate, DateOnly DueDate, string Responsible);
