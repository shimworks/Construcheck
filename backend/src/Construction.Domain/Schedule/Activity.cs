using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule;

public class Activity
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SchedulePhaseId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public DateRange PlannedPeriod { get; private set; } = null!;
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public ActivityStatus Status { get; private set; }
    public ActivityDeletionStatus DeletionStatus { get; private set; }

    private readonly List<Guid> _predecessorIds = [];
    public IReadOnlyList<Guid> PredecessorIds => _predecessorIds;

    private Activity() { }

    /// <summary>
    /// ProjectId é denormalizado a partir da SchedulePhase para permitir consultas
    /// eficientes por projeto (ex: recálculo em cascata) sem precisar navegar
    /// SchedulePhaseId -> SchedulePhase -> ProjectId a cada leitura. Como Activity nunca
    /// migra entre fases de projetos diferentes, esse campo nunca é alterado após a criação,
    /// eliminando o risco de dessincronia.
    /// </summary>
    public static Result<Activity> Create(
        Guid projectId, Guid schedulePhaseId, string name, int order, DateOnly plannedStart, DateOnly plannedEnd)
    {
        var periodResult = DateRange.Create(plannedStart, plannedEnd);
        if (periodResult.IsFailure)
            return Result<Activity>.Validation(periodResult.Error);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SchedulePhaseId = schedulePhaseId,
            Name = name,
            Order = order,
            PlannedPeriod = periodResult.Value!,
            Status = ActivityStatus.NotStarted,
            DeletionStatus = ActivityDeletionStatus.Active
        };

        return Result<Activity>.Success(activity);
    }

    public static Activity Reconstitute(
        Guid id, Guid projectId, Guid schedulePhaseId, string name, int order,
        DateOnly plannedStart, DateOnly plannedEnd,
        DateOnly? actualStart, DateOnly? actualEnd,
        ActivityStatus status, ActivityDeletionStatus deletionStatus,
        List<Guid> predecessorIds)
    {
        var activity = new Activity
        {
            Id = id,
            ProjectId = projectId,
            SchedulePhaseId = schedulePhaseId,
            Name = name,
            Order = order,
            PlannedPeriod = DateRange.FromExistingValues(plannedStart, plannedEnd),
            ActualStartDate = actualStart,
            ActualEndDate = actualEnd,
            Status = status,
            DeletionStatus = deletionStatus
        };

        activity._predecessorIds.AddRange(predecessorIds);
        return activity;
    }

    /// <summary>
    /// Adiciona uma dependência manual. A validação de que a data planejada desta atividade
    /// não é anterior ao fim planejado da predecessora é responsabilidade do Domain Service
    /// que orquestra a criação (ele precisa carregar a predecessora via Repository primeiro).
    /// </summary>
    public Result<bool> AddPredecessor(Guid predecessorId)
    {
        if (predecessorId == Id)
            return Result<bool>.Validation("Uma atividade não pode depender de si mesma.");

        if (_predecessorIds.Contains(predecessorId))
            return Result<bool>.Success(true); // idempotente

        _predecessorIds.Add(predecessorId);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Reatribui a posição de ordem desta atividade dentro da fase. A validação de que
    /// o conjunto completo de Orders permanece consistente (sem buracos, sem duplicata)
    /// é responsabilidade do Domain Service que orquestra a reordenação de todas as
    /// atividades da fase de uma vez.
    /// </summary>
    public void ReorderTo(int newOrder) => Order = newOrder;

    /// <summary>
    /// Inicia a atividade. Exige que a fase anterior (por Order) esteja concluída e que
    /// todas as predecessoras diretas (via Dependency manual) estejam concluídas.
    /// Essas duas condições são calculadas fora da entidade (pelo Domain Service) e passadas
    /// aqui já resolvidas, porque Activity não tem acesso a outros Aggregates.
    /// </summary>
    public Result<bool> Start(bool previousPhaseCompleted, bool allPredecessorsCompleted)
    {
        if (Status != ActivityStatus.NotStarted)
            return Result<bool>.Validation("A atividade já foi iniciada ou concluída.");

        if (!previousPhaseCompleted)
            return Result<bool>.Validation("A fase anterior ainda não foi concluída.");

        if (!allPredecessorsCompleted)
            return Result<bool>.Validation("Existem atividades predecessoras pendentes.");

        Status = ActivityStatus.InProgress;
        ActualStartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        return Result<bool>.Success(true);
    }

    public Result<ActivityCompletionOutcome> Complete()
    {
        if (Status != ActivityStatus.InProgress)
            return Result<ActivityCompletionOutcome>.Validation("Somente atividades em andamento podem ser concluídas.");

        var completionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var wasLate = completionDate > PlannedPeriod.End;

        Status = ActivityStatus.Completed;
        ActualEndDate = completionDate;

        return Result<ActivityCompletionOutcome>.Success(new ActivityCompletionOutcome(wasLate, completionDate));
    }


    /// <summary>
    /// Reagenda o período planejado da atividade. Usado pelo Domain Service de recálculo
    /// em cascata quando uma predecessora atrasa.
    /// </summary>
    public Result<bool> Reschedule(DateOnly newPlannedStart, DateOnly newPlannedEnd)
    {
        var periodResult = DateRange.Create(newPlannedStart, newPlannedEnd);
        if (periodResult.IsFailure)
            return Result<bool>.Validation(periodResult.Error);

        PlannedPeriod = periodResult.Value!;
        return Result<bool>.Success(true);
    }

    public void Remove() => DeletionStatus = ActivityDeletionStatus.Removed;
}
