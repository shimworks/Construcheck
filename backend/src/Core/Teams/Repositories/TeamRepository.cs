using Construcheck.Core.Data;
using Construcheck.Core.Teams.Entities;
using Construcheck.Core.Teams.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Core.Teams.Repositories;

public class TeamRepository(ICoreDbContext db) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Teams.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<Team>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.Teams.Where(e => e.ProjectId == projectId).OrderBy(e => e.Name).ToListAsync(ct);

    public async Task AddAsync(Team team, CancellationToken ct = default) =>
        await db.Teams.AddAsync(team, ct);

    public Task DeleteAsync(Team team, CancellationToken ct = default)
    {
        db.Teams.Remove(team);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}