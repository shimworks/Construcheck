using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule.DomainServices;

public class SchedulePhaseDeletionServiceTests
{
    private readonly IActivityRepository _activityRepository;
    private readonly SchedulePhaseDeletionService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public SchedulePhaseDeletionServiceTests()
    {
        _activityRepository = Substitute.For<IActivityRepository>();
        _sut = new SchedulePhaseDeletionService(_activityRepository);
    }

    private static Activity BuildActivity(Guid phaseId, ActivityDeletionStatus deletionStatus)
    {
        var activity = Activity.Create(ProjectId, phaseId, "Atividade", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)).Value!;
        if (deletionStatus == ActivityDeletionStatus.Removed)
            activity.Remove();
        return activity;
    }

    // -------------------------------------------------------------------------
    // TryRemoveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryRemoveAsync_PhaseHasActiveActivity_ReturnsValidationFailureAndDoesNotRemovePhase()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var activeActivity = BuildActivity(phase.Id, ActivityDeletionStatus.Active);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>())
                            .Returns([activeActivity]);

        // Act
        var result = await _sut.TryRemoveAsync(phase);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Existem atividades ativas nesta fase. Remova-as antes de remover a fase.", result.Error);
        Assert.Equal(SchedulePhaseDeletionStatus.Active, phase.DeletionStatus);
    }

    [Fact]
    public async Task TryRemoveAsync_PhaseHasOnlyRemovedActivities_ReturnsSuccessAndRemovesPhase()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var removedActivity = BuildActivity(phase.Id, ActivityDeletionStatus.Removed);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>())
                            .Returns([removedActivity]);

        // Act
        var result = await _sut.TryRemoveAsync(phase);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
    }

    [Fact]
    public async Task TryRemoveAsync_PhaseHasNoActivities_ReturnsSuccessAndRemovesPhase()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.TryRemoveAsync(phase);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
    }

    [Fact]
    public async Task TryRemoveAsync_MixOfActiveAndRemovedActivities_ReturnsValidationFailure()
    {
        // Arrange — combinação: uma removida e uma ativa juntas; a ativa deve bloquear
        // mesmo com a outra já removida
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var removedActivity = BuildActivity(phase.Id, ActivityDeletionStatus.Removed);
        var activeActivity = BuildActivity(phase.Id, ActivityDeletionStatus.Active);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>())
                            .Returns([removedActivity, activeActivity]);

        // Act
        var result = await _sut.TryRemoveAsync(phase);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SchedulePhaseDeletionStatus.Active, phase.DeletionStatus);
    }
}
