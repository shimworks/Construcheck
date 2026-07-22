using Construcheck.Core.Schedule.Entities;
using Construcheck.Core.Schedule.Interfaces;
using Construcheck.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Core.Schedule.Repositories;

public class ScheduleRepository(ICoreDbContext db) : IScheduleRepository
{
    public Task<SchedulePhase?> GetPhaseByIdAsync(Guid id, CancellationToken ct = default) =>
        db.SchedulePhases.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<SchedulePhase>> GetPhasesByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        db.SchedulePhases.Where(s => s.ProjectId == projectId).OrderBy(s => s.Order).ToListAsync(ct);

    public async Task AddPhaseAsync(SchedulePhase phase, CancellationToken ct = default) =>
        await db.SchedulePhases.AddAsync(phase, ct);

    public async Task AddPhasesAsync(IEnumerable<SchedulePhase> phases, CancellationToken ct = default) =>
        await db.SchedulePhases.AddRangeAsync(phases, ct);

    public Task<Activity?> GetActivityByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<Activity>> GetActivitiesByPhaseIdAsync(Guid phaseId, CancellationToken ct = default) =>
        db.Activities.Where(a => a.SchedulePhaseId == phaseId).ToListAsync(ct);

    public Task<List<Activity>> GetActivitiesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        db.Activities.Where(a => ids.Contains(a.Id)).ToListAsync(ct);

    public Task<List<Activity>> GetDependentActivitiesOfAsync(Guid activityId, CancellationToken ct = default) =>
        db.Dependencies
          .Where(d => d.PredecessorActivityId == activityId)
          .Join(db.Activities, d => d.ActivityId, a => a.Id, (d, a) => a)
          .ToListAsync(ct);

    public async Task AddActivityAsync(Activity activity, CancellationToken ct = default) =>
        await db.Activities.AddAsync(activity, ct);

    public Task<List<Dependency>> GetActivityDependenciesAsync(Guid activityId, CancellationToken ct = default) =>
        db.Dependencies.Where(d => d.ActivityId == activityId).ToListAsync(ct);

    public async Task AddDependencyAsync(Dependency dependency, CancellationToken ct = default) =>
        await db.Dependencies.AddAsync(dependency, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}