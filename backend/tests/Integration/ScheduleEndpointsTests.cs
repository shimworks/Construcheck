using Construcheck.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Construction;

public class ScheduleEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpRequestMessage> AuthorizedRequest(HttpMethod method, string url, bool asAdmin = true)
    {
        var token = asAdmin
            ? await factory.CreateAdminAndGetTokenAsync()
            : await factory.RegisterUserAndGetTokenAsync();

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> CreateProjectAsync()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        request.Content = JsonContent.Create(new
        {
            name = $"Obra Base {Guid.NewGuid()}",
            address = "Endereço",
            technicalManager = "Gestor",
            startDate = "2026-01-01",
            targetEndDate = "2026-12-31"
        });
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);
        return body.GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> SeedScheduleAsync(string projectId)
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/schedule/seed");
        var response = await _client.SendAsync(request);
        return await ReadJson(response);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/schedule/seed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Seed_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/schedule/seed", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Seed_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/schedule/seed");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seed_ShouldReturn200WithTenPhases_WhenProjectHasNoSchedule()
    {
        // Arrange
        var projectId = await CreateProjectAsync();

        // Act
        var body = await SeedScheduleAsync(projectId);

        // Assert
        Assert.Equal(10, body.GetArrayLength());
        Assert.Equal("Fundação", body[0].GetProperty("name").GetString());
        Assert.Equal("Entrega", body[9].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Seed_ShouldReturn409_WhenProjectAlreadyHasSchedule()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        await SeedScheduleAsync(projectId);

        var secondSeedRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/schedule/seed");

        // Act
        var response = await _client.SendAsync(secondSeedRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{projectId}/schedule
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProject_ShouldReturn200WithSeededPhases_WhenCalledByViewer()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        await SeedScheduleAsync(projectId);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/schedule", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10, body.GetProperty("phases").GetArrayLength());
    }

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/schedule/phases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePhase_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/schedule/phases");
        request.Content = JsonContent.Create(new { name = "Fase Extra", order = 11 });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePhase_ShouldReturn200AndPersist_WhenProjectExists()
    {
        // Arrange — projeto sem seed, criando fase avulsa diretamente
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/schedule/phases");
        request.Content = JsonContent.Create(new { name = "Fase Única", order = 1 });

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Fase Única", body.GetProperty("name").GetString());
    }

    // -------------------------------------------------------------------------
    // DELETE /api/phases/{phaseId}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemovePhase_ShouldReturn404_WhenPhaseDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Delete, $"/api/phases/{Guid.NewGuid()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemovePhase_ShouldReturn400_WhenPhaseHasActiveActivity()
    {
        // Arrange — o seed já cria fases COM atividades ativas; tentar remover a primeira
        // deve ser bloqueado por SchedulePhaseDeletionService
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhaseId = phases[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Delete, $"/api/phases/{firstPhaseId}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovePhase_ShouldReturn200_WhenPhaseHasNoActivities()
    {
        // Arrange — fase criada avulsa (sem seed), sem nenhuma atividade
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/schedule/phases");
        createRequest.Content = JsonContent.Create(new { name = "Fase Vazia", order = 1 });
        var createResponse = await _client.SendAsync(createRequest);
        var phaseId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var deleteRequest = await AuthorizedRequest(HttpMethod.Delete, $"/api/phases/{phaseId}");

        // Act
        var response = await _client.SendAsync(deleteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var phase = db.SchedulePhases.First(p => p.Id == Guid.Parse(phaseId!));
        Assert.Equal(Construcheck.Construction.Domain.Schedule.SchedulePhaseDeletionStatus.Removed, phase.DeletionStatus);
    }

    // -------------------------------------------------------------------------
    // POST /api/phases/{phaseId}/activities
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateActivity_ShouldReturn404_WhenPhaseDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/phases/{Guid.NewGuid()}/activities");
        request.Content = JsonContent.Create(new { name = "Atividade", plannedStartDate = "2026-01-01", plannedEndDate = "2026-01-10" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateActivity_ShouldAssignNextSequentialOrder_WhenPhaseAlreadyHasActivities()
    {
        // Arrange — a primeira fase do seed ("Fundação") já vem com 4 atividades
        // (Order 1-4); a próxima criada manualmente deve receber Order 5
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhaseId = phases[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/phases/{firstPhaseId}/activities");
        request.Content = JsonContent.Create(new { name = "Atividade Extra", plannedStartDate = "2026-01-01", plannedEndDate = "2026-01-10" });

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var created = db.Activities.First(a => a.Id == Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.Equal(5, created.Order);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/activities/{id}/iniciar — a regra de ordem entre fases é o ponto crítico
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartActivity_ShouldReturn200_WhenActivityBelongsToFirstPhase()
    {
        // Arrange — Order=1 não tem fase anterior; GetPreviousPhaseAsync retorna null,
        // e "previousPhase is null" conta como "completo" no domain service
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstActivityId = phases[0].GetProperty("activities")[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{firstActivityId}/iniciar");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var activity = db.Activities.First(a => a.Id == Guid.Parse(firstActivityId!));
        Assert.Equal(Construcheck.Construction.Domain.Schedule.ActivityStatus.InProgress, activity.Status);

        // A fase também deve ter sido marcada InProgress como efeito colateral (MarkInProgress)
        var phase = db.SchedulePhases.First(p => p.Id == activity.SchedulePhaseId);
        Assert.Equal(Construcheck.Construction.Domain.Schedule.PhaseStatus.InProgress, phase.Status);
    }

    [Fact]
    public async Task StartActivity_ShouldReturn400_WhenPreviousPhaseIsNotCompleted()
    {
        // Arrange — atividade da SEGUNDA fase ("Estrutura", Order=2); a primeira fase
        // ("Fundação") ainda está NotStarted, então deve bloquear
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var secondPhaseFirstActivityId = phases[1].GetProperty("activities")[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{secondPhaseFirstActivityId}/iniciar");

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("A fase anterior ainda não foi concluída.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StartActivity_ShouldReturn404_WhenActivityDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{Guid.NewGuid()}/iniciar");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/activities/{id}/concluir — incluindo a cascata de atraso, ponta a ponta
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteActivity_ShouldReturn400_WhenActivityIsNotInProgress()
    {
        // Arrange — atividade recém-criada pelo seed, ainda NotStarted (nunca passou por Start)
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var activityId = phases[0].GetProperty("activities")[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{activityId}/concluir");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteActivity_ShouldReturn200_WhenCompletedOnTime()
    {
        // Arrange — as datas placeholder do seed são sempre "hoje"; completar hoje
        // nunca é atraso (completionDate > PlannedPeriod.End é falso quando ambos são hoje)
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var activityId = phases[0].GetProperty("activities")[0].GetProperty("id").GetString();

        var startRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{activityId}/iniciar");
        await _client.SendAsync(startRequest);

        var completeRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{activityId}/concluir");

        // Act
        var response = await _client.SendAsync(completeRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var activity = db.Activities.First(a => a.Id == Guid.Parse(activityId!));
        Assert.Equal(Construcheck.Construction.Domain.Schedule.ActivityStatus.Completed, activity.Status);
    }

    [Fact]
    public async Task CompleteActivity_ShouldCascadeDelayToDependent_WhenCompletedLate()
    {
        // Arrange — o achado central deste fluxo: o seed sozinho NUNCA produz uma atividade
        // atrasável (datas placeholder = hoje sempre). Para exercitar a cascata de atraso de
        // verdade, é preciso reagendar manualmente a atividade predecessora para o passado
        // via UpdateActivityDetails ANTES de iniciar e completar — reproduzindo o cenário
        // real de "planejamento desatualizado", não um artefato de teste.
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhaseId = phases[0].GetProperty("id").GetString();
        var predecessorId = phases[0].GetProperty("activities")[0].GetProperty("id").GetString();

        // Reagenda a predecessora para um período já vencido
        var rescheduleRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/activities/{predecessorId}");
        rescheduleRequest.Content = JsonContent.Create(new
        {
            name = "Escavação",
            plannedStartDate = "2026-01-01",
            plannedEndDate = "2026-01-05"
        });
        await _client.SendAsync(rescheduleRequest);

        // Cria uma dependente cujo início planejado é logo após o fim (agora vencido) da predecessora
        var createDependentRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/phases/{firstPhaseId}/activities");
        createDependentRequest.Content = JsonContent.Create(new
        {
            name = "Dependente",
            plannedStartDate = "2026-01-05",
            plannedEndDate = "2026-01-10"
        });
        var dependentResponse = await _client.SendAsync(createDependentRequest);
        var dependentId = (await ReadJson(dependentResponse)).GetProperty("id").GetString();

        var addPredecessorRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/activities/{dependentId}/dependencies");
        addPredecessorRequest.Content = JsonContent.Create(new { predecessorActivityId = predecessorId });
        var addPredecessorResponse = await _client.SendAsync(addPredecessorRequest);
        Assert.Equal(HttpStatusCode.OK, addPredecessorResponse.StatusCode); // pré-condição do próprio teste

        using var dbBefore = factory.CreateConstructionDbContext();
        var originalDependentStart = dbBefore.Activities.First(a => a.Id == Guid.Parse(dependentId!)).PlannedPeriod.Start;

        // Inicia e completa a predecessora — sua data planejada já venceu (2026-01-05, no passado)
        var startRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{predecessorId}/iniciar");
        await _client.SendAsync(startRequest);

        var completeRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{predecessorId}/concluir");

        // Act
        var completeResponse = await _client.SendAsync(completeRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using var dbAfter = factory.CreateConstructionDbContext();
        var dependentAfter = dbAfter.Activities.First(a => a.Id == Guid.Parse(dependentId!));
        Assert.NotEqual(originalDependentStart, dependentAfter.PlannedPeriod.Start);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/phases/{phaseId}/activities/reorder
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Reorder_ShouldReturn400_WhenActivityIdsDoNotMatchPhaseActivities()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhaseId = phases[0].GetProperty("id").GetString();

        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/phases/{firstPhaseId}/activities/reorder");
        request.Content = JsonContent.Create(new { activityIdsInOrder = new[] { Guid.NewGuid() } });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_ShouldPersistNewOrder_WhenActivityIdsAreValid()
    {
        // Arrange — a primeira fase ("Fundação") vem com 4 atividades do seed:
        // Escavação(1), Estacas(2), Blocos(3), Baldrames(4)
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhase = phases[0];
        var firstPhaseId = firstPhase.GetProperty("id").GetString();
        var activityIds = firstPhase.GetProperty("activities").EnumerateArray()
            .Select(a => a.GetProperty("id").GetString()!).ToList();

        var reversedOrder = activityIds.AsEnumerable().Reverse().ToList();
        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/phases/{firstPhaseId}/activities/reorder");
        request.Content = JsonContent.Create(new { activityIdsInOrder = reversedOrder });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var lastActivityNowFirst = db.Activities.First(a => a.Id == Guid.Parse(reversedOrder[0]));
        Assert.Equal(1, lastActivityNowFirst.Order);
    }

    // -------------------------------------------------------------------------
    // POST /api/activities/{id}/dependencies
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddPredecessor_ShouldReturn400_WhenActivityStartsBeforePredecessorEnds()
    {
        // Arrange — dentro da mesma fase, a segunda atividade do seed ("Estacas") começa
        // no mesmo dia placeholder que a primeira ("Escavação") termina, então tentar
        // adicionar uma predecessora cujo fim é DEPOIS do início da dependente deve falhar.
        // Para forçar isso deliberadamente, reagenda a segunda atividade para começar
        // ANTES do fim planejado da primeira.
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var activities = phases[0].GetProperty("activities").EnumerateArray().ToList();
        var predecessorId = activities[0].GetProperty("id").GetString();
        var dependentId = activities[1].GetProperty("id").GetString();

        var rescheduleRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/activities/{predecessorId}");
        rescheduleRequest.Content = JsonContent.Create(new
        {
            name = "Escavação",
            plannedStartDate = "2026-01-01",
            plannedEndDate = "2026-01-20"
        });
        await _client.SendAsync(rescheduleRequest);

        var rescheduleDependentRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/activities/{dependentId}");
        rescheduleDependentRequest.Content = JsonContent.Create(new
        {
            name = "Estacas",
            plannedStartDate = "2026-01-10", // antes do fim (01-20) da predecessora
            plannedEndDate = "2026-01-25"
        });
        await _client.SendAsync(rescheduleDependentRequest);

        var addPredecessorRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/activities/{dependentId}/dependencies");
        addPredecessorRequest.Content = JsonContent.Create(new { predecessorActivityId = predecessorId });

        // Act
        var response = await _client.SendAsync(addPredecessorRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddPredecessor_ShouldReturn404_WhenEitherActivityDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/activities/{Guid.NewGuid()}/dependencies");
        request.Content = JsonContent.Create(new { predecessorActivityId = Guid.NewGuid() });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // KNOWN GAP (confirmado com o usuário, não é comportamento desejado):
    // nenhum endpoint HTTP aciona SchedulePhase.TryComplete(). Uma fase nunca avança
    // para Completed automaticamente, mesmo com todas as suas atividades concluídas —
    // o que bloqueia PERMANENTEMENTE o início de qualquer atividade da fase seguinte.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task KnownGap_SecondPhaseRemainsBlocked_EvenAfterAllFirstPhaseActivitiesAreCompleted()
    {
        // ESTE TESTE DOCUMENTA UM BUG CONHECIDO, NÃO UM COMPORTAMENTO DESEJADO.
        //
        // A regra de negócio pretendida (ver SchedulePhase.TryComplete e a checagem de
        // "previousPhaseCompleted" em ActivityStartValidationService) é: a segunda fase só
        // libera Start() de suas atividades depois que a primeira fase avançar para
        // PhaseStatus.Completed. Isso deveria acontecer quando a última atividade ativa
        // da fase é concluída — mas revisando ScheduleApplicationService, NENHUM método
        // público chama SchedulePhase.TryComplete(). MarkInProgress é acionado (via Start),
        // porém não existe trigger algum que avance a fase para Completed.
        //
        // CONSEQUÊNCIA: completar todas as atividades de uma fase NÃO a marca Completed.
        // A fase seguinte fica bloqueada PERMANENTEMENTE, mesmo com 100% do trabalho da
        // fase anterior concluído — nenhuma ação do usuário destrava isso hoje.
        //
        // TODO(produção): quando um endpoint/trigger para TryComplete for implementado,
        // este teste vai FALHAR — e essa falha é o sinal correto de que a lacuna foi
        // fechada. Nesse momento, inverter a asserção final para Completed/200 e
        // renomear o teste, removendo o prefixo "KnownGap_".
        var projectId = await CreateProjectAsync();
        var phases = await SeedScheduleAsync(projectId);
        var firstPhaseActivities = phases[0].GetProperty("activities").EnumerateArray()
            .Select(a => a.GetProperty("id").GetString()!).ToList();
        var secondPhaseFirstActivityId = phases[1].GetProperty("activities")[0].GetProperty("id").GetString();

        // Completa TODAS as atividades da primeira fase
        foreach (var activityId in firstPhaseActivities)
        {
            var startRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{activityId}/iniciar");
            var startResponse = await _client.SendAsync(startRequest);
            Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode); // pré-condição do teste

            var completeRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{activityId}/concluir");
            var completeResponse = await _client.SendAsync(completeRequest);
            Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode); // pré-condição do teste
        }

        using var db = factory.CreateConstructionDbContext();
        var firstPhase = db.SchedulePhases.First(p => p.Id == Guid.Parse(phases[0].GetProperty("id").GetString()!));

        // Act — tenta iniciar a primeira atividade da segunda fase
        var startSecondPhaseRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/activities/{secondPhaseFirstActivityId}/iniciar");
        var response = await _client.SendAsync(startSecondPhaseRequest);

        // Assert — documenta o comportamento real: a fase continua InProgress (nunca
        // avança para Completed automaticamente), então a segunda fase permanece bloqueada
        Assert.Equal(Construcheck.Construction.Domain.Schedule.PhaseStatus.InProgress, firstPhase.Status);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
