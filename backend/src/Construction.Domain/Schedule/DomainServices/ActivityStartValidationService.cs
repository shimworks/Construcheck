using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Schedule.DomainServices;

/// <summary>
/// Resolve as duas invariantes de ordem que Activity.Start() exige, mas que Activity
/// sozinha não consegue verificar (exigem consultar outros Aggregates via Repository).
/// </summary>
public class ActivityStartValidationService(
    IActivityRepository activityRepository,
    ISchedulePhaseRepository phaseRepository)
{
    public async Task<Result<bool>> TryStartAsync(Activity activity, CancellationToken ct = default)
    {
        var phase = await phaseRepository.GetByIdAsync(activity.SchedulePhaseId, ct);
        if (phase is null)
            return Result<bool>.NotFound("Fase não encontrada.");

        var previousPhase = await phaseRepository.GetPreviousPhaseAsync(phase.ProjectId, phase.Order, ct);
        var previousPhaseCompleted = previousPhase is null || previousPhase.Status == PhaseStatus.Completed;

        var predecessors = await activityRepository.GetByIdsAsync(activity.PredecessorIds, ct);
        var allPredecessorsCompleted = predecessors.All(p => p.Status == ActivityStatus.Completed);

        var startResult = activity.Start(previousPhaseCompleted, allPredecessorsCompleted);
        if (startResult.IsFailure)
            return startResult;

        phase.MarkInProgress();

        return Result<bool>.Success(true);
    }
}
