using Construcheck.Construction.Application.Schedule.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Schedule.Interfaces;

public interface IScheduleApplicationService
{
    Task<Result<List<SchedulePhaseResponse>>> SeedDefaultWbsAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ScheduleResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<SchedulePhaseResponse>> CreatePhaseAsync(Guid projectId, CreateSchedulePhaseRequest request, CancellationToken ct = default);
    Task<Result<bool>> RemovePhaseAsync(Guid phaseId, CancellationToken ct = default);

    Task<Result<ActivityResponse>> CreateActivityAsync(Guid phaseId, CreateActivityRequest request, CancellationToken ct = default);
    Task<Result<ActivityResponse>> UpdateActivityDetailsAsync(Guid id, UpdateActivityDetailsRequest request, CancellationToken ct = default);
    Task<Result<bool>> RemoveActivityAsync(Guid id, CancellationToken ct = default);

    Task<Result<ActivityResponse>> StartActivityAsync(Guid id, CancellationToken ct = default);
    Task<Result<ActivityResponse>> CompleteActivityAsync(Guid id, CancellationToken ct = default);

    Task<Result<bool>> ReorderActivitiesAsync(Guid phaseId, ReorderActivitiesRequest request, CancellationToken ct = default);
    Task<Result<bool>> AddPredecessorAsync(Guid activityId, AddPredecessorRequest request, CancellationToken ct = default);
}
