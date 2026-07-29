using Construcheck.Construction.Application.Contracts.DTOs;
using Construcheck.Construction.Application.Contracts.Interfaces;
using Construcheck.Construction.Domain.Contracts;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Contracts.Services;

public class ContractApplicationService(IContractRepository repository, IProjectRepository projectRepository) : IContractApplicationService
{
    public async Task<Result<ContractResponse>> CreateAsync(Guid projectId, CreateContractRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<ContractResponse>.NotFound("Obra não encontrada.");

        var contractResult = Contract.Create(
            projectId, request.Type, request.CounterpartyName, request.Value,
            request.StartDate, request.DueDate, request.Responsible);

        if (contractResult.IsFailure)
            return Result<ContractResponse>.Validation(contractResult.Error);

        var contract = contractResult.Value!;

        await repository.AddAsync(contract, ct);
        await repository.SaveChangesAsync(ct);

        return Result<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<Result<List<ContractResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var contracts = await repository.GetByProjectIdAsync(projectId, ct);
        return Result<List<ContractResponse>>.Success(contracts.Select(ToResponse).ToList());
    }

    public async Task<Result<ContractResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await repository.GetByIdAsync(id, ct);
        return contract is null
            ? Result<ContractResponse>.NotFound("Contrato não encontrado.")
            : Result<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<Result<ContractResponse>> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken ct = default)
    {
        var contract = await repository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result<ContractResponse>.NotFound("Contrato não encontrado.");

        var updateResult = contract.UpdateDetails(
            request.Type, request.CounterpartyName, request.Value,
            request.StartDate, request.DueDate, request.Responsible);

        if (updateResult.IsFailure)
            return Result<ContractResponse>.Validation(updateResult.Error);

        await repository.SaveChangesAsync(ct);

        return Result<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await repository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result<bool>.NotFound("Contrato não encontrado.");

        contract.Remove();
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static ContractResponse ToResponse(Contract contract) => new(
        contract.Id, contract.ProjectId, contract.Type, contract.CounterpartyName, contract.Value.Amount,
        contract.Term.Start, contract.Term.End, contract.Responsible);
}
