using Construcheck.Core.Teams.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Teams.Interfaces;

public interface ITeamService
{
    Task<Result<TeamResponse>> CreateAsync(Guid projectId, CreateTeamRequest request, CancellationToken ct = default);
    Task<Result<List<TeamResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}