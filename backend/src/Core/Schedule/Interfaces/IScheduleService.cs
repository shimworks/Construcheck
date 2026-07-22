using Construcheck.Core.Schedule.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Schedule.Interfaces;

public interface IScheduleService
{
    Task<Result<List<SchedulePhaseResponse>>> SeedDefaultWbsAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ScheduleResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<SchedulePhaseResponse>> CreatePhaseAsync(Guid projectId, CreateSchedulePhaseRequest request, CancellationToken ct = default);
    Task<Result<ActivityResponse>> CreateActivityAsync(Guid phaseId, CreateActivityRequest request, CancellationToken ct = default);
    Task<Result<ActivityResponse>> UpdateActivityAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default);
    Task<Result<bool>> ReorderActivitiesAsync(Guid phaseId, ReorderActivitiesRequest request, CancellationToken ct = default);
    Task<Result<bool>> AddDependencyAsync(Guid activityId, CreateDependencyRequest request, CancellationToken ct = default);
}