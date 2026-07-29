using Construcheck.Construction.Domain.Contracts;

namespace Construcheck.Construction.Application.Contracts.DTOs;

public record CreateContractRequest(
    ContractType Type, string CounterpartyName, decimal Value,
    DateOnly StartDate, DateOnly DueDate, string Responsible);
