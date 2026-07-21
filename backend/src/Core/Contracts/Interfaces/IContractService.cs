using Construcheck.Core.Contracts.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Contracts.Interfaces;

public interface IContractService
{
    Task<Result<ContractResponse>> CreateAsync(Guid projectId, CreateContractRequest request, CancellationToken ct = default);
    Task<Result<List<ContractResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ContractResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ContractResponse>> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}