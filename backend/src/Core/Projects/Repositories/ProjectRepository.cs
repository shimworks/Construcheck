using Construcheck.Core.Data;
using Construcheck.Core.Projects.Entities;
using Construcheck.Core.Projects.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Core.Projects.Repositories;

public class ProjectRepository(ICoreDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Project>> GetAllAsync(CancellationToken ct = default) =>
        db.Projects.OrderBy(o => o.Name).ToListAsync(ct);

    public async Task AddAsync(Project project, CancellationToken ct = default) =>
        await db.Projects.AddAsync(project, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}