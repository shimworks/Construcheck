using Construcheck.Construction.Application.Teams.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Teams.Interfaces;

public interface ITeamApplicationService
{
    Task<Result<TeamResponse>> CreateAsync(Guid projectId, CreateTeamRequest request, CancellationToken ct = default);
    Task<Result<List<TeamResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default);
}
