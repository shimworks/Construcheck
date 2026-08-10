using Construcheck.Construction.Domain.Schedule;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule;

public class SchedulePhaseTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsNotStartedPhaseWithActiveDeletionStatus()
    {
        // Act
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);

        // Assert
        Assert.NotEqual(Guid.Empty, phase.Id);
        Assert.Equal(ProjectId, phase.ProjectId);
        Assert.Equal("Fundação", phase.Name);
        Assert.Equal(1, phase.Order);
        Assert.Equal(PhaseStatus.NotStarted, phase.Status);
        Assert.Equal(SchedulePhaseDeletionStatus.Active, phase.DeletionStatus);
    }

    // -------------------------------------------------------------------------
    // Reconstitute
    // -------------------------------------------------------------------------

    [Fact]
    public void Reconstitute_ValidData_RestoresAllFieldsExactly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var phase = SchedulePhase.Reconstitute(
            id, ProjectId, "Estrutura", 2, PhaseStatus.InProgress, SchedulePhaseDeletionStatus.Removed);

        // Assert
        Assert.Equal(id, phase.Id);
        Assert.Equal(PhaseStatus.InProgress, phase.Status);
        Assert.Equal(SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
    }

    // -------------------------------------------------------------------------
    // TryComplete
    // -------------------------------------------------------------------------

    [Fact]
    public void TryComplete_NoActivities_ReturnsValidationFailure()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);

        // Act
        var result = phase.TryComplete([]);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Esta fase não possui atividades cadastradas.", result.Error);
        Assert.Equal(PhaseStatus.NotStarted, phase.Status);
    }

    [Fact]
    public void TryComplete_SomeActivitiesPending_ReturnsValidationFailureAndKeepsCurrentStatus()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Estrutura", 2);
        var statuses = new[] { ActivityStatus.Completed, ActivityStatus.InProgress };

        // Act
        var result = phase.TryComplete(statuses);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Existem atividades pendentes nesta fase.", result.Error);
        Assert.Equal(PhaseStatus.NotStarted, phase.Status);
    }

    [Fact]
    public void TryComplete_AllActivitiesCompleted_ReturnsSuccessAndSetsStatusToCompleted()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Alvenaria", 3);
        var statuses = new[] { ActivityStatus.Completed, ActivityStatus.Completed, ActivityStatus.Completed };

        // Act
        var result = phase.TryComplete(statuses);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PhaseStatus.Completed, phase.Status);
    }

    [Fact]
    public void TryComplete_SingleCompletedActivity_ReturnsSuccess()
    {
        // Arrange — fronteira: uma única atividade, não degenerada por estar vazia,
        // mas o menor caso não-vazio possível
        var phase = SchedulePhase.Create(ProjectId, "Cobertura", 4);

        // Act
        var result = phase.TryComplete([ActivityStatus.Completed]);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PhaseStatus.Completed, phase.Status);
    }

    // -------------------------------------------------------------------------
    // MarkInProgress
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkInProgress_WhenNotStarted_SetsStatusToInProgress()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);

        // Act
        phase.MarkInProgress();

        // Assert
        Assert.Equal(PhaseStatus.InProgress, phase.Status);
    }

    [Fact]
    public void MarkInProgress_WhenAlreadyCompleted_DoesNotRevertStatus()
    {
        // Arrange — a atividade radius secundário: garante que MarkInProgress não
        // sobrescreve um estado terminal já alcançado (só age a partir de NotStarted)
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        phase.TryComplete([ActivityStatus.Completed]);

        // Act
        phase.MarkInProgress();

        // Assert
        Assert.Equal(PhaseStatus.Completed, phase.Status);
    }

    [Fact]
    public void MarkInProgress_WhenAlreadyInProgress_RemainsInProgress()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);
        phase.MarkInProgress();

        // Act
        phase.MarkInProgress();

        // Assert
        Assert.Equal(PhaseStatus.InProgress, phase.Status);
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ActivePhase_SetsDeletionStatusToRemoved()
    {
        // Arrange
        var phase = SchedulePhase.Create(ProjectId, "Fundação", 1);

        // Act
        phase.Remove();

        // Assert
        Assert.Equal(SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
    }
}
