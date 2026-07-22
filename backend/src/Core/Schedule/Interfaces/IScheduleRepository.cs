using Construcheck.Core.Schedule.Entities;

namespace Construcheck.Core.Schedule.Interfaces;

public interface IScheduleRepository
{
    Task<SchedulePhase?> GetPhaseByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SchedulePhase>> GetPhasesByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddPhaseAsync(SchedulePhase phase, CancellationToken ct = default);
    Task AddPhasesAsync(IEnumerable<SchedulePhase> phases, CancellationToken ct = default);

    Task<Activity?> GetActivityByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Activity>> GetActivitiesByPhaseIdAsync(Guid phaseId, CancellationToken ct = default);
    Task<List<Activity>> GetActivitiesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Activity>> GetDependentActivitiesOfAsync(Guid activityId, CancellationToken ct = default);
    Task AddActivityAsync(Activity activity, CancellationToken ct = default);

    Task<List<Dependency>> GetActivityDependenciesAsync(Guid activityId, CancellationToken ct = default);
    Task AddDependencyAsync(Dependency dependency, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}