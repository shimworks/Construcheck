using Construcheck.Construction.Application.Schedule.DTOs;
using Construcheck.Construction.Application.Schedule.Interfaces;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.Data;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Schedule.Services;

public class ScheduleApplicationService(
    ISchedulePhaseRepository phaseRepository,
    IActivityRepository activityRepository,
    IProjectRepository projectRepository,
    ActivityStartValidationService startValidationService,
    ActivityCascadeRescheduleService cascadeRescheduleService,
    ActivityReorderService reorderService,
    SchedulePhaseDeletionService phaseDeletionService) : IScheduleApplicationService
{
    public async Task<Result<List<SchedulePhaseResponse>>> SeedDefaultWbsAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<List<SchedulePhaseResponse>>.NotFound("Obra não encontrada.");

        var existing = await phaseRepository.GetByProjectIdAsync(projectId, ct);
        if (existing.Count > 0)
            return Result<List<SchedulePhaseResponse>>.Conflict(
                "Esta obra já tem um cronograma. O seed só se aplica a obras sem etapas cadastradas.");

        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(projectId);

        await phaseRepository.AddRangeAsync(phases, ct);
        foreach (var activity in activities)
            await activityRepository.AddAsync(activity, ct);

        await phaseRepository.SaveChangesAsync(ct);

        var response = new List<SchedulePhaseResponse>();
        foreach (var phase in phases)
            response.Add(await ToPhaseResponseAsync(phase, ct));

        return Result<List<SchedulePhaseResponse>>.Success(response);
    }

    public async Task<Result<SchedulePhaseResponse>> CreatePhaseAsync(Guid projectId, CreateSchedulePhaseRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<SchedulePhaseResponse>.NotFound("Obra não encontrada.");

        var phase = SchedulePhase.Create(projectId, request.Name, request.Order);

        await phaseRepository.AddAsync(phase, ct);
        await phaseRepository.SaveChangesAsync(ct);

        return Result<SchedulePhaseResponse>.Success(await ToPhaseResponseAsync(phase, ct));
    }

    public async Task<Result<bool>> RemovePhaseAsync(Guid phaseId, CancellationToken ct = default)
    {
        var phase = await phaseRepository.GetByIdAsync(phaseId, ct);
        if (phase is null)
            return Result<bool>.NotFound("Fase não encontrada.");

        var removeResult = await phaseDeletionService.TryRemoveAsync(phase, ct);
        if (removeResult.IsFailure)
            return removeResult;

        await phaseRepository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<ActivityResponse>> CreateActivityAsync(Guid phaseId, CreateActivityRequest request, CancellationToken ct = default)
    {
        var phase = await phaseRepository.GetByIdAsync(phaseId, ct);
        if (phase is null)
            return Result<ActivityResponse>.NotFound("Etapa não encontrada.");

        var existingActivities = await activityRepository.GetByPhaseIdAsync(phaseId, ct);
        var nextOrder = existingActivities.Count == 0
            ? 1
            : existingActivities.Where(a => a.DeletionStatus == ActivityDeletionStatus.Active).Max(a => a.Order) + 1;

        var activityResult = Activity.Create(
            phase.ProjectId, phaseId, request.Name, nextOrder, request.PlannedStartDate, request.PlannedEndDate);

        if (activityResult.IsFailure)
            return Result<ActivityResponse>.Validation(activityResult.Error);

        var activity = activityResult.Value!;

        await activityRepository.AddAsync(activity, ct);
        await activityRepository.SaveChangesAsync(ct);

        return Result<ActivityResponse>.Success(ToActivityResponse(activity));
    }

    public async Task<Result<ActivityResponse>> UpdateActivityDetailsAsync(Guid id, UpdateActivityDetailsRequest request, CancellationToken ct = default)
    {
        var activity = await activityRepository.GetByIdAsync(id, ct);
        if (activity is null)
            return Result<ActivityResponse>.NotFound("Atividade não encontrada.");

        var rescheduleResult = activity.Reschedule(request.PlannedStartDate, request.PlannedEndDate);
        if (rescheduleResult.IsFailure)
            return Result<ActivityResponse>.Validation(rescheduleResult.Error);

        await activityRepository.SaveChangesAsync(ct);

        return Result<ActivityResponse>.Success(ToActivityResponse(activity));
    }

    public async Task<Result<bool>> RemoveActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await activityRepository.GetByIdAsync(id, ct);
        if (activity is null)
            return Result<bool>.NotFound("Atividade não encontrada.");

        activity.Remove();
        await activityRepository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<ActivityResponse>> StartActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await activityRepository.GetByIdAsync(id, ct);
        if (activity is null)
            return Result<ActivityResponse>.NotFound("Atividade não encontrada.");

        var startResult = await startValidationService.TryStartAsync(activity, ct);
        if (startResult.IsFailure)
            return Result<ActivityResponse>.Validation(startResult.Error);

        await activityRepository.SaveChangesAsync(ct);
        await phaseRepository.SaveChangesAsync(ct);

        return Result<ActivityResponse>.Success(ToActivityResponse(activity));
    }

    public async Task<Result<ActivityResponse>> CompleteActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await activityRepository.GetByIdAsync(id, ct);
        if (activity is null)
            return Result<ActivityResponse>.NotFound("Atividade não encontrada.");

        var completeResult = activity.Complete();
        if (completeResult.IsFailure)
            return Result<ActivityResponse>.Validation(completeResult.Error);

        await activityRepository.SaveChangesAsync(ct);

        // Recálculo em cascata: só dispara se a conclusão real ficou depois do previsto (atraso).
        // "wasLate" e "completionDate" vêm do próprio outcome de Complete(), calculados uma única
        // vez dentro da entidade — não recalculados aqui.
        var outcome = completeResult.Value!;
        if (outcome.WasLate)
        {
            var cascadeResult = await cascadeRescheduleService.RecalculateAsync(activity.ProjectId, activity.Id, outcome.CompletionDate, ct);
            if (cascadeResult.IsFailure)
                return Result<ActivityResponse>.Validation(cascadeResult.Error);

            await activityRepository.SaveChangesAsync(ct);
        }

        return Result<ActivityResponse>.Success(ToActivityResponse(activity));
    }

    public async Task<Result<bool>> ReorderActivitiesAsync(Guid phaseId, ReorderActivitiesRequest request, CancellationToken ct = default)
    {
        var reorderResult = await reorderService.ReorderAsync(phaseId, request.ActivityIdsInOrder, ct);
        if (reorderResult.IsFailure)
            return reorderResult;

        await activityRepository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> AddPredecessorAsync(Guid activityId, AddPredecessorRequest request, CancellationToken ct = default)
    {
        var activity = await activityRepository.GetByIdAsync(activityId, ct);
        var predecessor = await activityRepository.GetByIdAsync(request.PredecessorActivityId, ct);

        if (activity is null || predecessor is null)
            return Result<bool>.NotFound("Atividade ou predecessora não encontrada.");

        if (activity.PlannedPeriod.Start < predecessor.PlannedPeriod.End)
            return Result<bool>.Validation(
                "A atividade não pode iniciar antes do fim previsto da predecessora. Ajuste as datas antes de criar a dependência.");

        var addResult = activity.AddPredecessor(request.PredecessorActivityId);
        if (addResult.IsFailure)
            return addResult;

        await activityRepository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<ScheduleResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var phases = await phaseRepository.GetByProjectIdAsync(projectId, ct);

        var phasesResponse = new List<SchedulePhaseResponse>();
        foreach (var phase in phases)
            phasesResponse.Add(await ToPhaseResponseAsync(phase, ct));

        return Result<ScheduleResponse>.Success(new ScheduleResponse(phasesResponse));
    }

    private async Task<SchedulePhaseResponse> ToPhaseResponseAsync(SchedulePhase phase, CancellationToken ct)
    {
        var activities = await activityRepository.GetByPhaseIdAsync(phase.Id, ct);
        var orderedActivities = activities
            .Where(a => a.DeletionStatus == ActivityDeletionStatus.Active)
            .OrderBy(a => a.Order)
            .Select(ToActivityResponse)
            .ToList();

        return new SchedulePhaseResponse(phase.Id, phase.Name, phase.Order, phase.Status, orderedActivities);
    }

    private static ActivityResponse ToActivityResponse(Activity activity) => new(
        activity.Id, activity.SchedulePhaseId, activity.Name,
        activity.PlannedPeriod.Start, activity.PlannedPeriod.End,
        activity.ActualStartDate, activity.ActualEndDate, activity.Status,
        activity.PredecessorIds.ToList());
}
