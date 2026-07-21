using Construcheck.Core.Contracts.DTOs;
using Construcheck.Core.Contracts.Entities;
using Construcheck.Core.Contracts.Interfaces;
using Construcheck.Core.Projects.Interfaces;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Contracts.Services;

public class ContractService(IContractRepository repository, IProjectRepository projectRepository) : IContractService
{
    public async Task<Result<ContractResponse>> CreateAsync(Guid projectId, CreateContractRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<ContractResponse>.NotFound("Obra não encontrada.");

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = request.Type,
            CounterpartyName = request.CounterpartyName,
            Value = request.Value,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            Responsible = request.Responsible,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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

        contract.Type = request.Type;
        contract.CounterpartyName = request.CounterpartyName;
        contract.Value = request.Value;
        contract.StartDate = request.StartDate;
        contract.DueDate = request.DueDate;
        contract.Responsible = request.Responsible;
        contract.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(ct);

        return Result<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await repository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result<bool>.NotFound("Contrato não encontrado.");

        await repository.DeleteAsync(contract, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static ContractResponse ToResponse(Contract contract) => new(
        contract.Id, contract.ProjectId, contract.Type, contract.CounterpartyName, contract.Value,
        contract.StartDate, contract.DueDate, contract.Responsible);
}