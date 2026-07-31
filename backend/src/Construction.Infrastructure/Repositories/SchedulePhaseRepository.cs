using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Construction.Infrastructure.Repositories;

public class SchedulePhaseRepository(ConstructionDbContext db) : ISchedulePhaseRepository
{
    public Task<SchedulePhase?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.SchedulePhases.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<SchedulePhase>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.SchedulePhases
          .Where(s => s.ProjectId == projectId && s.DeletionStatus == SchedulePhaseDeletionStatus.Active)
          .OrderBy(s => s.Order)
          .ToListAsync(ct);

    public Task<SchedulePhase?> GetPreviousPhaseAsync(Guid projectId, int currentOrder, CancellationToken ct = default) =>
        db.SchedulePhases
          .Where(s => s.ProjectId == projectId
                   && s.DeletionStatus == SchedulePhaseDeletionStatus.Active
                   && s.Order < currentOrder)
          .OrderByDescending(s => s.Order)
          .FirstOrDefaultAsync(ct);

    public async Task AddAsync(SchedulePhase phase, CancellationToken ct = default) =>
        await db.SchedulePhases.AddAsync(phase, ct);

    public async Task AddRangeAsync(IEnumerable<SchedulePhase> phases, CancellationToken ct = default) =>
        await db.SchedulePhases.AddRangeAsync(phases, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
