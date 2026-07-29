using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule.DomainServices;

/// <summary>
/// Reordena as Activities de uma SchedulePhase. Garante que a nova ordem proposta
/// contém exatamente os mesmos IDs das atividades atuais da fase (sem faltar, sem
/// duplicar, sem incluir atividade de outra fase) antes de aplicar.
/// </summary>
public class ActivityReorderService(IActivityRepository activityRepository)
{
    public async Task<Result<bool>> ReorderAsync(
        Guid schedulePhaseId, List<Guid> activityIdsInNewOrder, CancellationToken ct = default)
    {
        var activities = await activityRepository.GetByPhaseIdAsync(schedulePhaseId, ct);
        var activeActivities = activities.Where(a => a.DeletionStatus == ActivityDeletionStatus.Active).ToList();

        var currentIds = activeActivities.Select(a => a.Id).OrderBy(id => id).ToList();
        var proposedIds = activityIdsInNewOrder.OrderBy(id => id).ToList();

        if (!currentIds.SequenceEqual(proposedIds))
            return Result<bool>.Validation("A lista de ordenação não bate com as atividades ativas da etapa.");

        var activityById = activeActivities.ToDictionary(a => a.Id);

        for (var i = 0; i < activityIdsInNewOrder.Count; i++)
            activityById[activityIdsInNewOrder[i]].ReorderTo(i + 1);

        return Result<bool>.Success(true);
    }
}
