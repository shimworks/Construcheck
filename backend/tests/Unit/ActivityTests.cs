using Construcheck.Construction.Domain.Schedule;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule;

public class ActivityTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid PhaseId = Guid.NewGuid();

    private static Activity CreateActivity(DateOnly? start = null, DateOnly? end = null) =>
        Activity.Create(
            ProjectId, PhaseId, "Escavação", 1,
            start ?? new DateOnly(2026, 1, 1),
            end ?? new DateOnly(2026, 1, 10)).Value!;

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsSuccessWithNotStartedStatus()
    {
        // Act
        var result = Activity.Create(ProjectId, PhaseId, "Escavação", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        // Assert
        Assert.True(result.IsSuccess);
        var activity = result.Value!;
        Assert.Equal(ProjectId, activity.ProjectId);
        Assert.Equal(PhaseId, activity.SchedulePhaseId);
        Assert.Equal("Escavação", activity.Name);
        Assert.Equal(1, activity.Order);
        Assert.Equal(ActivityStatus.NotStarted, activity.Status);
        Assert.Equal(ActivityDeletionStatus.Active, activity.DeletionStatus);
        Assert.Null(activity.ActualStartDate);
        Assert.Null(activity.ActualEndDate);
        Assert.Empty(activity.PredecessorIds);
    }

    [Fact]
    public void Create_PlannedEndBeforePlannedStart_ReturnsValidationFailure()
    {
        // Act
        var result = Activity.Create(ProjectId, PhaseId, "Escavação", 1, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A data final não pode ser anterior à data inicial.", result.Error);
    }

    // -------------------------------------------------------------------------
    // Reconstitute
    // -------------------------------------------------------------------------

    [Fact]
    public void Reconstitute_ValidData_RestoresAllFieldsIncludingPredecessorIds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var predecessorId = Guid.NewGuid();

        // Act
        var activity = Activity.Reconstitute(
            id, ProjectId, PhaseId, "Vigas", 2,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 2), null,
            ActivityStatus.InProgress, ActivityDeletionStatus.Active,
            [predecessorId]);

        // Assert
        Assert.Equal(id, activity.Id);
        Assert.Equal(ActivityStatus.InProgress, activity.Status);
        Assert.Equal(new DateOnly(2026, 1, 2), activity.ActualStartDate);
        Assert.Null(activity.ActualEndDate);
        Assert.Single(activity.PredecessorIds);
        Assert.Contains(predecessorId, activity.PredecessorIds);
    }

    // -------------------------------------------------------------------------
    // AddPredecessor
    // -------------------------------------------------------------------------

    [Fact]
    public void AddPredecessor_ValidId_AddsToPredecessorIdsAndReturnsSuccess()
    {
        // Arrange
        var activity = CreateActivity();
        var predecessorId = Guid.NewGuid();

        // Act
        var result = activity.AddPredecessor(predecessorId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(predecessorId, activity.PredecessorIds);
    }

    [Fact]
    public void AddPredecessor_SelfReference_ReturnsValidationFailureAndDoesNotAdd()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        var result = activity.AddPredecessor(activity.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Uma atividade não pode depender de si mesma.", result.Error);
        Assert.Empty(activity.PredecessorIds);
    }

    [Fact]
    public void AddPredecessor_AlreadyExistingId_ReturnsSuccessWithoutDuplicating()
    {
        // Arrange — idempotência: adicionar o mesmo predecessor duas vezes não deve
        // duplicar a entrada na lista
        var activity = CreateActivity();
        var predecessorId = Guid.NewGuid();
        activity.AddPredecessor(predecessorId);

        // Act
        var result = activity.AddPredecessor(predecessorId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(activity.PredecessorIds);
    }

    // -------------------------------------------------------------------------
    // ReorderTo
    // -------------------------------------------------------------------------

    [Fact]
    public void ReorderTo_NewOrder_UpdatesOrderProperty()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        activity.ReorderTo(5);

        // Assert
        Assert.Equal(5, activity.Order);
    }

    // -------------------------------------------------------------------------
    // Start
    // -------------------------------------------------------------------------

    [Fact]
    public void Start_PreviousPhaseCompletedAndAllPredecessorsCompleted_ReturnsSuccessAndSetsInProgress()
    {
        // Arrange
        var activity = CreateActivity();
        var before = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.InProgress, activity.Status);
        Assert.NotNull(activity.ActualStartDate);
        Assert.True(activity.ActualStartDate!.Value >= before);
    }

    [Fact]
    public void Start_PreviousPhaseNotCompleted_ReturnsValidationFailureAndKeepsNotStarted()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        var result = activity.Start(previousPhaseCompleted: false, allPredecessorsCompleted: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A fase anterior ainda não foi concluída.", result.Error);
        Assert.Equal(ActivityStatus.NotStarted, activity.Status);
    }

    [Fact]
    public void Start_PredecessorsNotCompleted_ReturnsValidationFailure()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        var result = activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Existem atividades predecessoras pendentes.", result.Error);
        Assert.Equal(ActivityStatus.NotStarted, activity.Status);
    }

    [Fact]
    public void Start_PreviousPhaseAndPredecessorsBothIncomplete_ReturnsPreviousPhaseErrorFirst()
    {
        // Arrange — combinação de duas violações simultâneas: a checagem de fase
        // anterior vem antes da checagem de predecessoras no código-fonte
        var activity = CreateActivity();

        // Act
        var result = activity.Start(previousPhaseCompleted: false, allPredecessorsCompleted: false);

        // Assert
        Assert.Equal("A fase anterior ainda não foi concluída.", result.Error);
    }

    [Fact]
    public void Start_AlreadyInProgress_ReturnsValidationFailure()
    {
        // Arrange
        var activity = CreateActivity();
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Act
        var result = activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("A atividade já foi iniciada ou concluída.", result.Error);
    }

    [Fact]
    public void Start_AlreadyCompleted_ReturnsValidationFailure()
    {
        // Arrange
        var activity = CreateActivity();
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);
        activity.Complete();

        // Act
        var result = activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("A atividade já foi iniciada ou concluída.", result.Error);
    }

    // -------------------------------------------------------------------------
    // Complete
    // -------------------------------------------------------------------------

    [Fact]
    public void Complete_WhenNotStarted_ReturnsValidationFailure()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        var result = activity.Complete();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Somente atividades em andamento podem ser concluídas.", result.Error);
    }

    [Fact]
    public void Complete_WhenInProgressAndPlannedEndInFuture_ReturnsSuccessWithWasLateFalse()
    {
        // Arrange — planejado para terminar bem no futuro, então completar hoje não é atraso
        var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var activity = CreateActivity(end: futureEnd);
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Act
        var result = activity.Complete();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WasLate);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        Assert.NotNull(activity.ActualEndDate);
        Assert.Equal(result.Value.CompletionDate, activity.ActualEndDate);
    }

    [Fact]
    public void Complete_WhenInProgressAndPlannedEndInPast_ReturnsSuccessWithWasLateTrue()
    {
        // Arrange — planejado para terminar no passado; completar hoje é atraso.
        // PlannedPeriod exige Start <= End, então o Start também precisa estar no passado.
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var activity = CreateActivity(start: pastStart, end: pastEnd);
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);

        // Act
        var result = activity.Complete();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasLate);
    }

    [Fact]
    public void Complete_OutcomeCompletionDateMatchesTodayWithinTolerance()
    {
        // Arrange — verifica o observável secundário (CompletionDate do outcome),
        // não só o status final
        var activity = CreateActivity(end: DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1));
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);
        var expectedToday = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = activity.Complete();

        // Assert — tolerância de 1 dia para não quebrar por mudança de fuso/instante exato
        var diff = Math.Abs(result.Value!.CompletionDate.DayNumber - expectedToday.DayNumber);
        Assert.True(diff <= 1);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ReturnsValidationFailure()
    {
        // Arrange
        var activity = CreateActivity(end: DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1));
        activity.Start(previousPhaseCompleted: true, allPredecessorsCompleted: true);
        activity.Complete();

        // Act
        var result = activity.Complete();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Somente atividades em andamento podem ser concluídas.", result.Error);
    }

    // -------------------------------------------------------------------------
    // Reschedule
    // -------------------------------------------------------------------------

    [Fact]
    public void Reschedule_ValidDates_UpdatesPlannedPeriodAndReturnsSuccess()
    {
        // Arrange
        var activity = CreateActivity();
        var newStart = new DateOnly(2026, 3, 1);
        var newEnd = new DateOnly(2026, 3, 15);

        // Act
        var result = activity.Reschedule(newStart, newEnd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newStart, activity.PlannedPeriod.Start);
        Assert.Equal(newEnd, activity.PlannedPeriod.End);
    }

    [Fact]
    public void Reschedule_NewEndBeforeNewStart_ReturnsValidationFailureAndKeepsOriginalPeriod()
    {
        // Arrange
        var originalStart = new DateOnly(2026, 1, 1);
        var originalEnd = new DateOnly(2026, 1, 10);
        var activity = CreateActivity(originalStart, originalEnd);

        // Act
        var result = activity.Reschedule(new DateOnly(2026, 5, 1), new DateOnly(2026, 1, 1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(originalStart, activity.PlannedPeriod.Start);
        Assert.Equal(originalEnd, activity.PlannedPeriod.End);
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ActiveActivity_SetsDeletionStatusToRemoved()
    {
        // Arrange
        var activity = CreateActivity();

        // Act
        activity.Remove();

        // Assert
        Assert.Equal(ActivityDeletionStatus.Removed, activity.DeletionStatus);
    }
}
