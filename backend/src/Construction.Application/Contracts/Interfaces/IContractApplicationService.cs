using Construcheck.Construction.Application.Contracts.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Contracts.Interfaces;

public interface IContractApplicationService
{
    Task<Result<ContractResponse>> CreateAsync(Guid projectId, CreateContractRequest request, CancellationToken ct = default);
    Task<Result<List<ContractResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ContractResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ContractResponse>> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default);
}
