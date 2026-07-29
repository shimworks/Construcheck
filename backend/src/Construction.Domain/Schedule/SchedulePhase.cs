using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule;

public class SchedulePhase
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public PhaseStatus Status { get; private set; }
    public SchedulePhaseDeletionStatus DeletionStatus { get; private set; }

    private SchedulePhase() { }

    public static SchedulePhase Create(Guid projectId, string name, int order) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        Name = name,
        Order = order,
        Status = PhaseStatus.NotStarted,
        DeletionStatus = SchedulePhaseDeletionStatus.Active
    };

    public static SchedulePhase Reconstitute(
        Guid id, Guid projectId, string name, int order,
        PhaseStatus status, SchedulePhaseDeletionStatus deletionStatus) => new()
    {
        Id = id,
        ProjectId = projectId,
        Name = name,
        Order = order,
        Status = status,
        DeletionStatus = deletionStatus
    };

    /// <summary>
    /// Tenta avançar o status da fase para Completed. Exige que todas as Activities
    /// da fase estejam concluídas — essa checagem é feita fora (Domain Service),
    /// porque SchedulePhase não tem acesso direto às Activities (Aggregate separado).
    /// </summary>
    public Result<bool> TryComplete(IEnumerable<ActivityStatus> activityStatuses)
    {
        var statuses = activityStatuses.ToList();

        if (statuses.Count == 0)
            return Result<bool>.Validation("Esta fase não possui atividades cadastradas.");

        if (statuses.Any(s => s != ActivityStatus.Completed))
            return Result<bool>.Validation("Existem atividades pendentes nesta fase.");

        Status = PhaseStatus.Completed;
        return Result<bool>.Success(true);
    }

    public void MarkInProgress()
    {
        if (Status == PhaseStatus.NotStarted)
            Status = PhaseStatus.InProgress;
    }

    public void Remove() => DeletionStatus = SchedulePhaseDeletionStatus.Removed;
}
