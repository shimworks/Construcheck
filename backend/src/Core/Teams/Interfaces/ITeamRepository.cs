using Construcheck.Core.Teams.Entities;

namespace Construcheck.Core.Teams.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Team>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Team team, CancellationToken ct = default);
    Task DeleteAsync(Team team, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}