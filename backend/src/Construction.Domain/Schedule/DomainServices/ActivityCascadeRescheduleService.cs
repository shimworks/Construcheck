using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule.DomainServices;

/// <summary>
/// Propaga atraso em cascata: quando uma Activity termina depois do previsto,
/// empurra o início planejado de todas as suas dependentes diretas e indiretas.
/// Mantém um conjunto de IDs já visitados para detectar e recusar dependência circular,
/// em vez de estourar a pilha de recursão.
/// </summary>
public class ActivityCascadeRescheduleService(IActivityRepository activityRepository)
{
    public async Task<Result<bool>> RecalculateAsync(
        Guid projectId, Guid activityId, DateOnly newEndDate, CancellationToken ct = default)
    {
        var visited = new HashSet<Guid> { activityId };
        return await RecalculateInternalAsync(projectId, activityId, newEndDate, visited, ct);
    }

    private async Task<Result<bool>> RecalculateInternalAsync(
        Guid projectId, Guid activityId, DateOnly newEndDate, HashSet<Guid> visited, CancellationToken ct)
    {
        var dependents = await activityRepository.GetByPredecessorIdAsync(projectId, activityId, ct);

        foreach (var dependent in dependents)
        {
            if (!visited.Add(dependent.Id))
                return Result<bool>.Validation(
                    "Dependência circular detectada no cronograma. Corrija as dependências antes de continuar.");

            if (dependent.PlannedPeriod.Start >= newEndDate)
                continue; // já está OK, nada a propagar

            var delayDays = newEndDate.DayNumber - dependent.PlannedPeriod.Start.DayNumber;
            var newDependentEnd = dependent.PlannedPeriod.End.AddDays(delayDays);

            var rescheduleResult = dependent.Reschedule(newEndDate, newDependentEnd);
            if (rescheduleResult.IsFailure)
                return rescheduleResult;

            var cascadeResult = await RecalculateInternalAsync(projectId, dependent.Id, newDependentEnd, visited, ct);
            if (cascadeResult.IsFailure)
                return cascadeResult;
        }

        return Result<bool>.Success(true);
    }
}
