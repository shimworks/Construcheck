using Construcheck.Construction.Application.Schedule.DTOs;
using Construcheck.Construction.Application.Schedule.Services;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Application.Schedule;

public class ScheduleApplicationServiceTests
{
    private readonly ISchedulePhaseRepository _phaseRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ActivityStartValidationService _startValidationService;
    private readonly ActivityCascadeRescheduleService _cascadeRescheduleService;
    private readonly ActivityReorderService _reorderService;
    private readonly SchedulePhaseDeletionService _phaseDeletionService;
    private readonly ScheduleApplicationService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public ScheduleApplicationServiceTests()
    {
        _phaseRepository = Substitute.For<ISchedulePhaseRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();

        // Domain services concretos são resolvidos via seus próprios repositórios mockados,
        // não via NSubstitute direto sobre a classe — ScheduleApplicationService depende
        // dos TIPOS CONCRETOS (não interfaces), então instanciamos de verdade com repos mockados.
        _startValidationService = new ActivityStartValidationService(_activityRepository, _phaseRepository);
        _cascadeRescheduleService = new ActivityCascadeRescheduleService(_activityRepository);
        _reorderService = new ActivityReorderService(_activityRepository);
        _phaseDeletionService = new SchedulePhaseDeletionService(_activityRepository);

        _sut = new ScheduleApplicationService(
            _phaseRepository, _activityRepository, _projectRepository,
            _startValidationService, _cascadeRescheduleService, _reorderService, _phaseDeletionService);
    }

    private static Project BuildProject() =>
        Project.Create("Obra", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)).Value!;

    private static SchedulePhase BuildPhase(Guid projectId, int order = 1) =>
        SchedulePhase.Create(projectId, $"Fase {order}", order);

    private static Activity BuildActivity(Guid projectId, Guid phaseId, int order = 1, DateOnly? start = null, DateOnly? end = null) =>
        Activity.Create(projectId, phaseId, $"Atividade {order}", order,
            start ?? new DateOnly(2026, 1, 1), end ?? new DateOnly(2026, 1, 10)).Value!;

    // -------------------------------------------------------------------------
    // SeedDefaultWbsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedDefaultWbsAsync_ProjectNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.SeedDefaultWbsAsync(ProjectId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task SeedDefaultWbsAsync_ProjectAlreadyHasPhases_ReturnsConflictFailure()
    {
        // Arrange
        var project = BuildProject();
        var existingPhase = BuildPhase(project.Id);
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _phaseRepository.GetByProjectIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns([existingPhase]);

        // Act
        var result = await _sut.SeedDefaultWbsAsync(project.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Conflict, result.ErrorType);
        Assert.Equal("Esta obra já tem um cronograma. O seed só se aplica a obras sem etapas cadastradas.", result.Error);
    }

    [Fact]
    public async Task SeedDefaultWbsAsync_ProjectHasNoPhases_ReturnsSuccessWithTenPhases()
    {
        // Arrange — o template fixo em WbsTemplateSeed tem exatamente 10 fases
        var project = BuildProject();
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _phaseRepository.GetByProjectIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns([]);
        _activityRepository.GetByPhaseIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.SeedDefaultWbsAsync(project.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Count);
        await _phaseRepository.Received(1).AddRangeAsync(Arg.Any<IEnumerable<SchedulePhase>>(), Arg.Any<CancellationToken>());
        await _phaseRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // CreatePhaseAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePhaseAsync_ProjectNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var request = new CreateSchedulePhaseRequest("Fundação", 1);
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.CreatePhaseAsync(ProjectId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreatePhaseAsync_ProjectExists_ReturnsSuccessAndPersists()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateSchedulePhaseRequest("Fundação", 1);
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _activityRepository.GetByPhaseIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.CreatePhaseAsync(project.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Fundação", result.Value!.Name);
        await _phaseRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemovePhaseAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemovePhaseAsync_PhaseNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var phaseId = Guid.NewGuid();
        _phaseRepository.GetByIdAsync(phaseId, Arg.Any<CancellationToken>()).Returns((SchedulePhase?)null);

        // Act
        var result = await _sut.RemovePhaseAsync(phaseId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemovePhaseAsync_PhaseHasActiveActivity_DelegatesToDomainServiceAndReturnsValidationFailure()
    {
        // Arrange — verifica que o Application Service realmente delega para
        // SchedulePhaseDeletionService, não reimplementa a regra
        var phase = BuildPhase(ProjectId);
        var activeActivity = BuildActivity(ProjectId, phase.Id);
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([activeActivity]);

        // Act
        var result = await _sut.RemovePhaseAsync(phase.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal(SchedulePhaseDeletionStatus.Active, phase.DeletionStatus);
        await _phaseRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemovePhaseAsync_PhaseHasNoActiveActivities_ReturnsSuccessAndPersists()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.RemovePhaseAsync(phase.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
        await _phaseRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // CreateActivityAsync — foco no cálculo de nextOrder
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateActivityAsync_PhaseNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var phaseId = Guid.NewGuid();
        var request = new CreateActivityRequest("Escavação", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        _phaseRepository.GetByIdAsync(phaseId, Arg.Any<CancellationToken>()).Returns((SchedulePhase?)null);

        // Act
        var result = await _sut.CreateActivityAsync(phaseId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateActivityAsync_PhaseHasNoActivities_AssignsOrderOne()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        var request = new CreateActivityRequest("Escavação", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.CreateActivityAsync(phase.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        await _activityRepository.Received(1).AddAsync(
            Arg.Is<Activity>(a => a.Order == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateActivityAsync_PhaseHasActiveActivities_AssignsOrderAsMaxPlusOne()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        var existing1 = BuildActivity(ProjectId, phase.Id, order: 1);
        var existing2 = BuildActivity(ProjectId, phase.Id, order: 3); // maior order ativo é 3
        var request = new CreateActivityRequest("Nova Atividade", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([existing1, existing2]);

        // Act
        var result = await _sut.CreateActivityAsync(phase.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        await _activityRepository.Received(1).AddAsync(
            Arg.Is<Activity>(a => a.Order == 4), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateActivityAsync_IgnoresRemovedActivitiesWhenCalculatingNextOrder()
    {
        // Arrange — atividade removida com Order alto não deve ser usada no Max();
        // apenas ativas contam
        var phase = BuildPhase(ProjectId);
        var active = BuildActivity(ProjectId, phase.Id, order: 1);
        var removed = BuildActivity(ProjectId, phase.Id, order: 99);
        removed.Remove();
        var request = new CreateActivityRequest("Nova Atividade", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([active, removed]);

        // Act
        var result = await _sut.CreateActivityAsync(phase.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        await _activityRepository.Received(1).AddAsync(
            Arg.Is<Activity>(a => a.Order == 2), Arg.Any<CancellationToken>()); // max(1) + 1, ignora 99
    }

    [Fact]
    public async Task CreateActivityAsync_InvalidDateRange_ReturnsValidationFailure()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        var request = new CreateActivityRequest("Escavação", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1));
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.CreateActivityAsync(phase.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    // -------------------------------------------------------------------------
    // UpdateActivityDetailsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateActivityDetailsAsync_ActivityNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateActivityDetailsRequest("Nome", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        _activityRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Activity?)null);

        // Act
        var result = await _sut.UpdateActivityDetailsAsync(id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateActivityDetailsAsync_InvalidDateRange_ReturnsValidationFailure()
    {
        // Arrange — cobre o caminho de falha que a auditoria encontrou sem teste:
        // Activity.Reschedule falha com Validation quando end < start, e
        // ScheduleApplicationService reembrulha isso como Result<ActivityResponse>.Validation
        var activity = BuildActivity(ProjectId, Guid.NewGuid());
        var request = new UpdateActivityDetailsRequest("Nome", new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1));
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.UpdateActivityDetailsAsync(activity.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _activityRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateActivityDetailsAsync_ValidData_ReschedulesAndPersists()
    {
        // Arrange
        var activity = BuildActivity(ProjectId, Guid.NewGuid());
        var request = new UpdateActivityDetailsRequest("Nome Novo", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 10));
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.UpdateActivityDetailsAsync(activity.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 2, 1), activity.PlannedPeriod.Start);
        await _activityRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemoveActivityAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveActivityAsync_ActivityNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _activityRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Activity?)null);

        // Act
        var result = await _sut.RemoveActivityAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveActivityAsync_ActivityExists_RemovesAndPersists()
    {
        // Arrange
        var activity = BuildActivity(ProjectId, Guid.NewGuid());
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.RemoveActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityDeletionStatus.Removed, activity.DeletionStatus);
    }

    // -------------------------------------------------------------------------
    // StartActivityAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartActivityAsync_ActivityNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _activityRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Activity?)null);

        // Act
        var result = await _sut.StartActivityAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task StartActivityAsync_ValidationFailsInDomainService_ReturnsValidationFailure()
    {
        // Arrange — fase da atividade não é encontrada pelo domain service real injetado.
        // ActivityStartValidationService.TryStartAsync retorna NotFound internamente, mas
        // ScheduleApplicationService.StartActivityAsync SEMPRE reembrulha qualquer falha
        // vinda de TryStartAsync como Result<ActivityResponse>.Validation(startResult.Error) —
        // não repassa o ErrorType original. A mensagem de texto sobrevive; o ErrorType não.
        var activity = BuildActivity(ProjectId, Guid.NewGuid());
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _phaseRepository.GetByIdAsync(activity.SchedulePhaseId, Arg.Any<CancellationToken>()).Returns((SchedulePhase?)null);

        // Act
        var result = await _sut.StartActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Fase não encontrada.", result.Error);
    }

    [Fact]
    public async Task StartActivityAsync_AllConditionsMet_ReturnsSuccessAndPersistsBothRepositories()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        var activity = BuildActivity(ProjectId, phase.Id);
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _phaseRepository.GetByIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns(phase);
        _phaseRepository.GetPreviousPhaseAsync(ProjectId, phase.Order, Arg.Any<CancellationToken>()).Returns((SchedulePhase?)null);
        _activityRepository.GetByIdsAsync(activity.PredecessorIds, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.StartActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.InProgress, activity.Status);
        await _activityRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _phaseRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // CompleteActivityAsync — a lógica mais crítica: cascata condicional em WasLate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteActivityAsync_ActivityNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _activityRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Activity?)null);

        // Act
        var result = await _sut.CompleteActivityAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CompleteActivityAsync_ActivityNotInProgress_ReturnsValidationFailure()
    {
        // Arrange — status NotStarted, Complete() do domínio recusa
        var activity = BuildActivity(ProjectId, Guid.NewGuid());
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.CompleteActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CompleteActivityAsync_CompletedOnTime_DoesNotTriggerCascadeRecalculation()
    {
        // Arrange — planejado para terminar no futuro distante: completar agora não é atraso.
        // Verificamos que GetByPredecessorIdAsync NUNCA é chamado, provando que a cascata
        // foi genuinamente pulada (não apenas que ela "não quebrou nada").
        var phase = BuildPhase(ProjectId);
        var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var activity = BuildActivity(ProjectId, phase.Id, end: futureEnd);
        activity.Start(true, true);
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.CompleteActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        await _activityRepository.DidNotReceive().GetByPredecessorIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteActivityAsync_CompletedLate_TriggersCascadeRecalculationForDependents()
    {
        // Arrange — planejado para terminar no passado: completar agora É atraso,
        // deve disparar ActivityCascadeRescheduleService.RecalculateAsync, que por sua vez
        // chama GetByPredecessorIdAsync. Este é o teste-chave da interação condicional.
        var phase = BuildPhase(ProjectId);
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var activity = BuildActivity(ProjectId, phase.Id, start: pastStart, end: pastEnd);
        activity.Start(true, true);
        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, activity.Id, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.CompleteActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        await _activityRepository.Received(1).GetByPredecessorIdAsync(
            ProjectId, activity.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteActivityAsync_LateCompletionCascadesDelayToDependent()
    {
        // Arrange — verifica o efeito ponta-a-ponta: atraso real empurra a data
        // planejada de uma dependente real, através de toda a cadeia de chamadas
        var phase = BuildPhase(ProjectId);
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var activity = BuildActivity(ProjectId, phase.Id, start: pastStart, end: pastEnd);
        activity.Start(true, true);

        var dependent = BuildActivity(ProjectId, phase.Id, order: 2, start: pastEnd.AddDays(-5), end: pastEnd.AddDays(2));
        var originalDependentStart = dependent.PlannedPeriod.Start;

        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, activity.Id, Arg.Any<CancellationToken>()).Returns([dependent]);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent.Id, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.CompleteActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(originalDependentStart, dependent.PlannedPeriod.Start);
    }

    [Fact]
    public async Task CompleteActivityAsync_CascadeDetectsCircularDependency_ReturnsValidationFailure()
    {
        // Arrange — a cascata falha (ciclo detectado); o Application Service deve
        // propagar essa falha em vez de reportar sucesso silenciosamente
        var phase = BuildPhase(ProjectId);
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var activity = BuildActivity(ProjectId, phase.Id, start: pastStart, end: pastEnd);
        activity.Start(true, true);

        var dependent = BuildActivity(ProjectId, phase.Id, order: 2, start: pastEnd.AddDays(-5), end: pastEnd.AddDays(2));

        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _activityRepository.GetByPredecessorIdAsync(ProjectId, activity.Id, Arg.Any<CancellationToken>()).Returns([dependent]);
        // dependent aponta de volta pra si mesmo, criando o ciclo
        _activityRepository.GetByPredecessorIdAsync(ProjectId, dependent.Id, Arg.Any<CancellationToken>()).Returns([dependent]);

        // Act
        var result = await _sut.CompleteActivityAsync(activity.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Dependência circular detectada no cronograma. Corrija as dependências antes de continuar.", result.Error);
    }

    // -------------------------------------------------------------------------
    // ReorderActivitiesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReorderActivitiesAsync_MismatchedIds_ReturnsValidationFailure()
    {
        // Arrange
        var phaseId = Guid.NewGuid();
        var activity = BuildActivity(ProjectId, phaseId);
        var request = new ReorderActivitiesRequest([Guid.NewGuid()]); // Id inexistente
        _activityRepository.GetByPhaseIdAsync(phaseId, Arg.Any<CancellationToken>()).Returns([activity]);

        // Act
        var result = await _sut.ReorderActivitiesAsync(phaseId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ReorderActivitiesAsync_ValidIds_ReturnsSuccessAndPersists()
    {
        // Arrange
        var phaseId = Guid.NewGuid();
        var activity1 = BuildActivity(ProjectId, phaseId, order: 1);
        var activity2 = BuildActivity(ProjectId, phaseId, order: 2);
        var request = new ReorderActivitiesRequest([activity2.Id, activity1.Id]);
        _activityRepository.GetByPhaseIdAsync(phaseId, Arg.Any<CancellationToken>()).Returns([activity1, activity2]);

        // Act
        var result = await _sut.ReorderActivitiesAsync(phaseId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, activity2.Order);
        Assert.Equal(2, activity1.Order);
        await _activityRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // AddPredecessorAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddPredecessorAsync_ActivityOrPredecessorNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var predecessorId = Guid.NewGuid();
        var request = new AddPredecessorRequest(predecessorId);
        _activityRepository.GetByIdAsync(activityId, Arg.Any<CancellationToken>()).Returns((Activity?)null);
        _activityRepository.GetByIdAsync(predecessorId, Arg.Any<CancellationToken>()).Returns((Activity?)null);

        // Act
        var result = await _sut.AddPredecessorAsync(activityId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Atividade ou predecessora não encontrada.", result.Error);
    }

    [Fact]
    public async Task AddPredecessorAsync_SelfReference_ReturnsValidationFailureWithoutRewrappingErrorType()
    {
        // Arrange — cobre o caminho de falha que a auditoria encontrou sem teste:
        // Activity.AddPredecessor recusa auto-referência com Validation, e
        // AddPredecessorAsync usa "return addResult;" (repassa sem reembrulhar) —
        // diferente de StartActivityAsync, que reembrulha.
        //
        // CUIDADO: a checagem de datas em AddPredecessorAsync ("Start < predecessor.End")
        // roda ANTES de AddPredecessor ser chamado. Com predecessor == activity (mesmo
        // objeto, apontando pra si mesma), isso vira "activity.Start < activity.End" —
        // que é verdadeiro para qualquer atividade com duração real, disparando a
        // mensagem de datas ANTES de alcançar a checagem de self-reference que este
        // teste quer provar. Por isso a atividade aqui tem Start == End (duração zero):
        // "Start < End" fica falso, a checagem de datas deixa passar, e só então
        // AddPredecessor roda de verdade e recusa por self-reference.
        var phaseId = Guid.NewGuid();
        var singleDay = new DateOnly(2026, 1, 10);
        var activity = BuildActivity(ProjectId, phaseId, order: 1, start: singleDay, end: singleDay);
        var request = new AddPredecessorRequest(activity.Id); // aponta pra si mesma

        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        // Act
        var result = await _sut.AddPredecessorAsync(activity.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Uma atividade não pode depender de si mesma.", result.Error);
        await _activityRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPredecessorAsync_ActivityStartsBeforePredecessorEnds_ReturnsValidationFailure()
    {
        // Arrange — a atividade planeja começar antes do fim planejado da predecessora
        var phaseId = Guid.NewGuid();
        var predecessor = BuildActivity(ProjectId, phaseId, order: 1, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 20));
        var activity = BuildActivity(ProjectId, phaseId, order: 2, start: new DateOnly(2026, 1, 10), end: new DateOnly(2026, 1, 25));
        var request = new AddPredecessorRequest(predecessor.Id);

        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _activityRepository.GetByIdAsync(predecessor.Id, Arg.Any<CancellationToken>()).Returns(predecessor);

        // Act
        var result = await _sut.AddPredecessorAsync(activity.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal(
            "A atividade não pode iniciar antes do fim previsto da predecessora. Ajuste as datas antes de criar a dependência.",
            result.Error);
        Assert.Empty(activity.PredecessorIds);
    }

    [Fact]
    public async Task AddPredecessorAsync_ActivityStartsExactlyWhenPredecessorEnds_ReturnsSuccess()
    {
        // Arrange — fronteira exata: Start == predecessor.End; a checagem usa "<",
        // então igualdade deve passar
        var phaseId = Guid.NewGuid();
        var predecessor = BuildActivity(ProjectId, phaseId, order: 1, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 10));
        var activity = BuildActivity(ProjectId, phaseId, order: 2, start: new DateOnly(2026, 1, 10), end: new DateOnly(2026, 1, 20));
        var request = new AddPredecessorRequest(predecessor.Id);

        _activityRepository.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        _activityRepository.GetByIdAsync(predecessor.Id, Arg.Any<CancellationToken>()).Returns(predecessor);

        // Act
        var result = await _sut.AddPredecessorAsync(activity.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(predecessor.Id, activity.PredecessorIds);
        await _activityRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetByProjectIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProjectIdAsync_ReturnsPhasesWithOnlyActiveActivitiesOrderedByOrder()
    {
        // Arrange
        var phase = BuildPhase(ProjectId);
        var activeActivity = BuildActivity(ProjectId, phase.Id, order: 2);
        var removedActivity = BuildActivity(ProjectId, phase.Id, order: 1);
        removedActivity.Remove();
        _phaseRepository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([phase]);
        _activityRepository.GetByPhaseIdAsync(phase.Id, Arg.Any<CancellationToken>()).Returns([activeActivity, removedActivity]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        var phaseResponse = Assert.Single(result.Value!.Phases);
        var activityResponse = Assert.Single(phaseResponse.Activities); // só a ativa
        Assert.Equal(activeActivity.Id, activityResponse.Id);
    }
}
