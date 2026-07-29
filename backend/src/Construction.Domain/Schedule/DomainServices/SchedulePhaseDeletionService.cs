using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule.DomainServices;

/// <summary>
/// Remove uma SchedulePhase, mas bloqueia a operação se existir qualquer Activity
/// ativa (não removida) pertencente a ela — conforme decisão de que remoção de
/// cronograma nunca deve apagar silenciosamente histórico de execução real.
/// </summary>
public class SchedulePhaseDeletionService(IActivityRepository activityRepository)
{
    public async Task<Result<bool>> TryRemoveAsync(SchedulePhase phase, CancellationToken ct = default)
    {
        var activities = await activityRepository.GetByPhaseIdAsync(phase.Id, ct);

        if (activities.Any(a => a.DeletionStatus == ActivityDeletionStatus.Active))
            return Result<bool>.Validation(
                "Existem atividades ativas nesta fase. Remova-as antes de remover a fase.");

        phase.Remove();
        return Result<bool>.Success(true);
    }
}
