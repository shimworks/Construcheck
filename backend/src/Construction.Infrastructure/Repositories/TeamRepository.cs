using Construcheck.Construction.Domain.Teams;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Construction.Infrastructure.Repositories;

public class TeamRepository(ConstructionDbContext db) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Teams.FirstOrDefaultAsync(t => t.Id == id && t.Status == TeamStatus.Active, ct);

    public Task<List<Team>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.Teams
          .Where(t => t.ProjectId == projectId && t.Status == TeamStatus.Active)
          .OrderBy(t => t.Name)
          .ToListAsync(ct);

    public async Task AddAsync(Team team, CancellationToken ct = default) =>
        await db.Teams.AddAsync(team, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
