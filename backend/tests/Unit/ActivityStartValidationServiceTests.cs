using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule.DomainServices;

public class ActivityStartValidationServiceTests
{
    private readonly IActivityRepository _activityRepository;
    private readonly ISchedulePhaseRepository _phaseRepository;
    private readonly ActivityStartValidationService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public ActivityStartValidationServiceTests()
    {
        _activityRepository = Substitute.For<IActivityRepository>();
        _phaseRepository = Substitute.For<ISchedulePhaseRepository>();
        _sut = new ActivityStartValidationService(_activityRepository, _phaseRepository);
    }

    private static Activity BuildActivity(Guid phaseId, List<Guid>? predecessorIds = null) =>
        Activity.Reconstitute(
            Guid.NewGuid(), ProjectId, phaseId, "Atividade", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10),
            null, null, ActivityStatus.NotStarted, ActivityDeletionStatus.Active,
            predecessorIds ?? []);

    // -------------------------------------------------------------------------
    // TryStartAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryStartAsync_PhaseNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var activity = BuildActivity(Guid.NewGuid());
        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>())
                         .Returns((SchedulePhase?)null);

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Fase não encontrada.", result.Error);
    }

    [Fact]
    public async Task TryStartAsync_NoPreviousPhaseAndNoPredecessors_ReturnsSuccessAndMarksPhaseInProgress()
    {
        // Arrange — primeira fase do projeto (sem fase anterior), sem predecessoras
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var activity = BuildActivity(phase.Id);

        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>())
                         .Returns((SchedulePhase?)null);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.InProgress, activity.Status);
        Assert.Equal(PhaseStatus.InProgress, phase.Status);
    }

    [Fact]
    public async Task TryStartAsync_PreviousPhaseNotCompleted_ReturnsValidationFailure()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Estrutura", 2);
        var previousPhase = SchedulePhase.Create(ProjectId, "Fundação", 1); // ainda NotStarted
        var activity = BuildActivity(phase.Id);

        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>())
                         .Returns(previousPhase);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A fase anterior ainda não foi concluída.", result.Error);
        Assert.Equal(ActivityStatus.NotStarted, activity.Status);
    }

    [Fact]
    public async Task TryStartAsync_PreviousPhaseCompleted_ReturnsSuccess()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Estrutura", 2);
        var previousPhase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        previousPhase.TryComplete([ActivityStatus.Completed]);
        var activity = BuildActivity(phase.Id);

        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>())
                         .Returns(previousPhase);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TryStartAsync_PredecessorNotCompleted_ReturnsValidationFailure()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var predecessor = Activity.Create(ProjectId, phase.Id, "Escavação", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)).Value!;
        var activity = BuildActivity(phase.Id, [predecessor.Id]);

        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>())
                         .Returns((SchedulePhase?)null);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>())
                            .Returns([predecessor]); // status NotStarted

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Existem atividades predecessoras pendentes.", result.Error);
    }

    [Fact]
    public async Task TryStartAsync_AllPredecessorsCompleted_ReturnsSuccess()
    {
        // Arrange — múltiplas predecessoras, todas concluídas
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        var predecessor1 = Activity.Create(ProjectId, phase.Id, "Escavação", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)).Value!;
        var predecessor2 = Activity.Create(ProjectId, phase.Id, "Estacas", 2, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)).Value!;
        predecessor1.Start(true, true);
        predecessor1.Complete();
        predecessor2.Start(true, true);
        predecessor2.Complete();

        var activity = BuildActivity(phase.Id, [predecessor1.Id, predecessor2.Id]);

        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>())
                         .Returns((SchedulePhase?)null);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>())
                            .Returns([predecessor1, predecessor2]);

        // Act
        var result = await _sut.TryStartAsync(activity);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
