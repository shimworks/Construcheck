using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule.DomainServices;

public class ActivityReorderServiceTests
{
    private readonly IActivityRepository _activityRepository;
    private readonly ActivityReorderService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid PhaseId = Guid.NewGuid();

    public ActivityReorderServiceTests()
    {
        _activityRepository = Substitute.For<IActivityRepository>();
        _sut = new ActivityReorderService(_activityRepository);
    }

    private static Activity BuildActivity(int order) =>
        Activity.Create(ProjectId, PhaseId, $"Atividade {order}", order, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)).Value!;

    // -------------------------------------------------------------------------
    // ReorderAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReorderAsync_ProposedIdsMatchCurrentActiveIds_ReturnsSuccessAndReassignsOrder()
    {
        // Arrange
        var activity1 = BuildActivity(1);
        var activity2 = BuildActivity(2);
        var activity3 = BuildActivity(3);
        _activityRepository.GetByPhaseIdAsync(PhaseId, Arg.Any<CancellationToken>())
                            .Returns([activity1, activity2, activity3]);

        var newOrder = new List<Guid> { activity3.Id, activity1.Id, activity2.Id };

        // Act
        var result = await _sut.ReorderAsync(PhaseId, newOrder);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, activity3.Order);
        Assert.Equal(2, activity1.Order);
        Assert.Equal(3, activity2.Order);
    }

    [Fact]
    public async Task ReorderAsync_ProposedIdsMissingOneActivity_ReturnsValidationFailure()
    {
        // Arrange
        var activity1 = BuildActivity(1);
        var activity2 = BuildActivity(2);
        _activityRepository.GetByPhaseIdAsync(PhaseId, Arg.Any<CancellationToken>())
                            .Returns([activity1, activity2]);

        var incompleteOrder = new List<Guid> { activity1.Id }; // falta activity2

        // Act
        var result = await _sut.ReorderAsync(PhaseId, incompleteOrder);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A lista de ordenação não bate com as atividades ativas da etapa.", result.Error);
    }

    [Fact]
    public async Task ReorderAsync_ProposedIdsIncludeUnknownId_ReturnsValidationFailure()
    {
        // Arrange
        var activity1 = BuildActivity(1);
        _activityRepository.GetByPhaseIdAsync(PhaseId, Arg.Any<CancellationToken>())
                            .Returns([activity1]);

        var orderWithForeignId = new List<Guid> { activity1.Id, Guid.NewGuid() };

        // Act
        var result = await _sut.ReorderAsync(PhaseId, orderWithForeignId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ReorderAsync_ExcludesRemovedActivitiesFromComparison()
    {
        // Arrange — atividade removida não deve entrar na comparação de conjunto,
        // então a lista proposta só precisa cobrir as ativas
        var activeActivity = BuildActivity(1);
        var removedActivity = BuildActivity(2);
        removedActivity.Remove();
        _activityRepository.GetByPhaseIdAsync(PhaseId, Arg.Any<CancellationToken>())
                            .Returns([activeActivity, removedActivity]);

        var newOrder = new List<Guid> { activeActivity.Id };

        // Act
        var result = await _sut.ReorderAsync(PhaseId, newOrder);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, activeActivity.Order);
    }

    [Fact]
    public async Task ReorderAsync_SingleActivity_ReturnsSuccess()
    {
        // Arrange — fronteira: menor caso não-degenerado, uma única atividade
        var activity = BuildActivity(1);
        _activityRepository.GetByPhaseIdAsync(PhaseId, Arg.Any<CancellationToken>())
                            .Returns([activity]);

        // Act
        var result = await _sut.ReorderAsync(PhaseId, [activity.Id]);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, activity.Order);
    }
}
