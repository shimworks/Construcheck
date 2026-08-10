using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule.DomainServices;

public class ActivityCascadeRescheduleServiceTests
{
    private readonly IActivityRepository _activityRepository;
    private readonly ActivityCascadeRescheduleService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid PhaseId = Guid.NewGuid();

    public ActivityCascadeRescheduleServiceTests()
    {
        _activityRepository = Substitute.For<IActivityRepository>();
        _sut = new ActivityCascadeRescheduleService(_activityRepository);
    }

    private static Activity BuildActivity(DateOnly plannedStart, DateOnly plannedEnd) =>
        Activity.Create(ProjectId, PhaseId, "Atividade", 1, plannedStart, plannedEnd).Value!;

    // -------------------------------------------------------------------------
    // RecalculateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecalculateAsync_NoDependents_ReturnsSuccessWithoutRescheduling()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityId, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityId, new DateOnly(2026, 2, 1));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RecalculateAsync_DependentAlreadyStartsAfterNewEndDate_SkipsRescheduling()
    {
        // Arrange — dependente já começa depois da nova data de fim: nada a propagar
        var activityId = Guid.NewGuid();
        var newEndDate = new DateOnly(2026, 2, 1);
        var dependent = BuildActivity(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10)); // já está OK
        var originalDependentStart = dependent.PlannedPeriod.Start;

        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityId, Arg.Any<CancellationToken>())
                            .Returns([dependent]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent.Id, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityId, newEndDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(originalDependentStart, dependent.PlannedPeriod.Start); // não foi tocado
    }

    [Fact]
    public async Task RecalculateAsync_DependentStartsBeforeNewEndDate_ReschedulesPreservingDuration()
    {
        // Arrange — dependente com 5 dias de duração planejada; ao ser empurrado,
        // a duração original deve ser preservada (delayDays aplicado a Start e End)
        var activityId = Guid.NewGuid();
        var newEndDate = new DateOnly(2026, 2, 1);
        var dependent = BuildActivity(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 20)); // 5 dias, começa antes do novo fim

        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityId, Arg.Any<CancellationToken>())
                            .Returns([dependent]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent.Id, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityId, newEndDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newEndDate, dependent.PlannedPeriod.Start);
        Assert.Equal(newEndDate.AddDays(5), dependent.PlannedPeriod.End); // duração de 5 dias preservada
    }

    [Fact]
    public async Task RecalculateAsync_MultiLevelChain_CascadesDelayThroughAllLevels()
    {
        // Arrange — cadeia A -> B -> C: atraso em A deve empurrar B, que por sua vez empurra C
        var activityId = Guid.NewGuid();
        var newEndDate = new DateOnly(2026, 2, 1);

        var dependentB = BuildActivity(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 20)); // 5 dias
        var dependentC = BuildActivity(new DateOnly(2026, 1, 18), new DateOnly(2026, 1, 25)); // 7 dias, também precisa empurrar

        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityId, Arg.Any<CancellationToken>())
                            .Returns([dependentB]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependentB.Id, Arg.Any<CancellationToken>())
                            .Returns([dependentC]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependentC.Id, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityId, newEndDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newEndDate, dependentB.PlannedPeriod.Start);
        Assert.Equal(newEndDate.AddDays(5), dependentB.PlannedPeriod.End);
        // C é empurrado a partir do NOVO fim de B, não do fim original de B
        Assert.Equal(dependentB.PlannedPeriod.End, dependentC.PlannedPeriod.Start);
    }

    [Fact]
    public async Task RecalculateAsync_MultipleDependentsOnSameActivity_ReschedulesAllOfThem()
    {
        // Arrange — uma atividade com duas dependentes diretas (ramificação, não cadeia linear)
        var activityId = Guid.NewGuid();
        var newEndDate = new DateOnly(2026, 2, 1);
        var dependent1 = BuildActivity(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15));
        var dependent2 = BuildActivity(new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 18));

        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityId, Arg.Any<CancellationToken>())
                            .Returns([dependent1, dependent2]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent1.Id, Arg.Any<CancellationToken>())
                            .Returns([]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent2.Id, Arg.Any<CancellationToken>())
                            .Returns([]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityId, newEndDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newEndDate, dependent1.PlannedPeriod.Start);
        Assert.Equal(newEndDate, dependent2.PlannedPeriod.Start);
    }

    [Fact]
    public async Task RecalculateAsync_CircularDependency_ReturnsValidationFailureInsteadOfStackOverflow()
    {
        // Arrange — ciclo: A depende de B e B depende de A (via GetByPredecessorIdAsync
        // mockado para se referenciar mutuamente). O HashSet `visited` deve detectar
        // isso na segunda visita ao mesmo Id, em vez de recursão infinita.
        var activityAId = Guid.NewGuid();
        var activityB = BuildActivity(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 20));

        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityAId, Arg.Any<CancellationToken>())
                            .Returns([activityB]);
        // B "depende" de volta de A — mas o serviço só enxerga o Id de B na recursão,
        // e A já está no `visited` inicial (semeado com o próprio activityId de entrada).
        // Para forçar o ciclo dentro da própria cadeia de dependentes, B aponta para
        // uma atividade cujo Id é o mesmo de B outra vez (auto-referência via mock).
        _activityRepository.GetByPredecessorIdAsync(ProjectId, activityB.Id, Arg.Any<CancellationToken>())
                            .Returns([activityB]);

        // Act
        var result = await _sut.RecalculateAsync(ProjectId, activityAId, new DateOnly(2026, 2, 1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Dependência circular detectada no cronograma. Corrija as dependências antes de continuar.", result.Error);
    }

}
