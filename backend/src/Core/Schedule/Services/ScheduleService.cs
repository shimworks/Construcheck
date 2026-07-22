using Construcheck.Core.Schedule.DTOs;
using Construcheck.Core.Schedule.Data;
using Construcheck.Core.Schedule.Entities;
using Construcheck.Core.Schedule.Enums;
using Construcheck.Core.Schedule.Interfaces;
using Construcheck.Core.Projects.Interfaces;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Schedule.Services;

public class ScheduleService(
    IScheduleRepository repository,
    IProjectRepository projectRepository) : IScheduleService
{
    public async Task<Result<List<SchedulePhaseResponse>>> SeedDefaultWbsAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<List<SchedulePhaseResponse>>.NotFound("Obra não encontrada.");

        var existing = await repository.GetPhasesByProjectIdAsync(projectId, ct);
        if (existing.Count > 0)
            return Result<List<SchedulePhaseResponse>>.Conflict(
                "Esta obra já tem um cronograma. O seed só se aplica a obras sem etapas cadastradas.");

        var phases = WbsTemplateSeed.CreateDefaultPhases(projectId);
        await repository.AddPhasesAsync(phases, ct);
        await repository.SaveChangesAsync(ct);

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

        var phase = new SchedulePhase
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name,
            Order = request.Order
        };

        await repository.AddPhaseAsync(phase, ct);
        await repository.SaveChangesAsync(ct);

        return Result<SchedulePhaseResponse>.Success(await ToPhaseResponseAsync(phase, ct));
    }

    public async Task<Result<ActivityResponse>> CreateActivityAsync(Guid phaseId, CreateActivityRequest request, CancellationToken ct = default)
    {
        var phase = await repository.GetPhaseByIdAsync(phaseId, ct);
        if (phase is null)
            return Result<ActivityResponse>.NotFound("Etapa não encontrada.");

        if (request.PlannedEndDate < request.PlannedStartDate)
            return Result<ActivityResponse>.Validation("Data de fim prevista não pode ser anterior à data de início.");

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            SchedulePhaseId = phaseId,
            Name = request.Name,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            Status = ActivityStatus.NotStarted
        };

        await repository.AddActivityAsync(activity, ct);
        await repository.SaveChangesAsync(ct);

        return Result<ActivityResponse>.Success(await ToActivityResponseAsync(activity, ct));
    }

    public async Task<Result<bool>> AddDependencyAsync(Guid activityId, CreateDependencyRequest request, CancellationToken ct = default)
    {
        var activity = await repository.GetActivityByIdAsync(activityId, ct);
        var predecessor = await repository.GetActivityByIdAsync(request.PredecessorActivityId, ct);

        if (activity is null || predecessor is null)
            return Result<bool>.NotFound("Atividade ou predecessora não encontrada.");

        if (activity.Id == predecessor.Id)
            return Result<bool>.Validation("Uma atividade não pode depender de si mesma.");

        if (activity.PlannedStartDate < predecessor.PlannedEndDate)
            return Result<bool>.Validation(
                "A atividade não pode iniciar antes do fim previsto da predecessora. Ajuste as datas antes de criar a dependência.");

        await repository.AddDependencyAsync(new Dependency
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            PredecessorActivityId = request.PredecessorActivityId
        }, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<ActivityResponse>> UpdateActivityAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default)
    {
        var activity = await repository.GetActivityByIdAsync(id, ct);
        if (activity is null)
            return Result<ActivityResponse>.NotFound("Atividade não encontrada.");

        // Valida contra o fim (real, se já aconteceu, ou previsto) das predecessoras diretas
        var dependencies = await repository.GetActivityDependenciesAsync(id, ct);
        var predecessors = await repository.GetActivitiesByIdsAsync(
            dependencies.Select(d => d.PredecessorActivityId), ct);

        var predecessorEndDates = predecessors.Select(p => p.ActualEndDate ?? p.PlannedEndDate).ToList();
        if (predecessorEndDates.Count > 0 && request.PlannedStartDate < predecessorEndDates.Max())
            return Result<ActivityResponse>.Validation(
                "Data de início não pode ser anterior ao fim (real ou previsto) de uma predecessora.");

        activity.Name = request.Name;
        activity.PlannedStartDate = request.PlannedStartDate;
        activity.PlannedEndDate = request.PlannedEndDate;
        activity.ActualStartDate = request.ActualStartDate;
        activity.ActualEndDate = request.ActualEndDate;
        activity.Status = request.Status;

        await repository.SaveChangesAsync(ct);

        // Recálculo em cascata: só dispara se o fim real ficou depois do previsto (atraso)
        if (activity.ActualEndDate is { } actualEnd && actualEnd > activity.PlannedEndDate)
            await RecalculateDependentsAsync(activity.Id, actualEnd, ct);

        return Result<ActivityResponse>.Success(await ToActivityResponseAsync(activity, ct));
    }

    private async Task RecalculateDependentsAsync(Guid activityId, DateOnly newEndDate, CancellationToken ct)
    {
        var dependents = await repository.GetDependentActivitiesOfAsync(activityId, ct);

        foreach (var dependent in dependents)
        {
            if (dependent.PlannedStartDate >= newEndDate)
                continue; // já está OK, nada a propagar

            var delayDays = newEndDate.DayNumber - dependent.PlannedStartDate.DayNumber;

            dependent.PlannedStartDate = newEndDate;
            dependent.PlannedEndDate = dependent.PlannedEndDate.AddDays(delayDays);

            // propaga recursivamente pra quem depende desta dependente
            await RecalculateDependentsAsync(dependent.Id, dependent.PlannedEndDate, ct);
        }

        await repository.SaveChangesAsync(ct);
    }

    public Task<Result<bool>> ReorderActivitiesAsync(Guid phaseId, ReorderActivitiesRequest request, CancellationToken ct = default)
    {
        // A ordem hoje é implícita pela posição na lista — não existe coluna `Order` em `activities`.
        // Este método só valida a lista recebida; se precisar persistir ordem explícita,
        // adicionar a coluna (ver seção de Pendências no fim do documento).
        return ValidateAndReorderAsync(phaseId, request, ct);
    }

    private async Task<Result<bool>> ValidateAndReorderAsync(Guid phaseId, ReorderActivitiesRequest request, CancellationToken ct)
    {
        var activities = await repository.GetActivitiesByPhaseIdAsync(phaseId, ct);

        if (activities.Count != request.ActivityIdsInOrder.Count ||
            activities.Select(a => a.Id).OrderBy(x => x).SequenceEqual(request.ActivityIdsInOrder.OrderBy(x => x)) is false)
            return Result<bool>.Validation("A lista de ordenação não bate com as atividades da etapa.");

        return Result<bool>.Success(true);
    }

    public async Task<Result<ScheduleResponse>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var phases = await repository.GetPhasesByProjectIdAsync(projectId, ct);

        var phasesResponse = new List<SchedulePhaseResponse>();
        foreach (var phase in phases)
            phasesResponse.Add(await ToPhaseResponseAsync(phase, ct));

        return Result<ScheduleResponse>.Success(
            new ScheduleResponse(phasesResponse));
    }

    private async Task<SchedulePhaseResponse> ToPhaseResponseAsync(SchedulePhase phase, CancellationToken ct)
    {
        var activities = await repository.GetActivitiesByPhaseIdAsync(phase.Id, ct);
        var activitiesResponse = new List<ActivityResponse>();
        foreach (var activity in activities)
            activitiesResponse.Add(await ToActivityResponseAsync(activity, ct));

        return new SchedulePhaseResponse(phase.Id, phase.Name, phase.Order, activitiesResponse);
    }

    private async Task<ActivityResponse> ToActivityResponseAsync(Activity activity, CancellationToken ct)
    {
        var dependencies = await repository.GetActivityDependenciesAsync(activity.Id, ct);

        return new ActivityResponse(
            activity.Id, activity.SchedulePhaseId, activity.Name,
            activity.PlannedStartDate, activity.PlannedEndDate,
            activity.ActualStartDate, activity.ActualEndDate, activity.Status,
            dependencies.Select(d => d.PredecessorActivityId).ToList());
    }
}