using Construcheck.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Construction;

public class TeamsEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<string> CreateProjectAsync(string namePrefix = "Obra Base")
    {
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        request.Content = JsonContent.Create(new
        {
            name = $"{namePrefix} {Guid.NewGuid()}",
            address = "Endereço",
            technicalManager = "Gestor",
            startDate = "2026-01-01",
            targetEndDate = "2026-12-31"
        });
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);
        return body.GetProperty("id").GetString()!;
    }

    private static object BuildCreateTeamRequest(string name = "Equipe Teste") => new
    {
        name,
        specialty = "Elétrica",
        memberCount = 5
    };

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/teams
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/teams", BuildCreateTeamRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenCalledByViewer()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/teams", asAdmin: false);
        request.Content = JsonContent.Create(BuildCreateTeamRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenProjectDoesNotExist()
    {
        // Arrange — diferente de ProjectsController, TeamsController.Create depende
        // de um projectId de rota que precisa existir; testa o NotFound do
        // TeamApplicationService.CreateAsync propagado através do HTTP
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/teams");
        request.Content = JsonContent.Create(BuildCreateTeamRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn200AndPersist_WhenProjectExistsAndDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams");
        request.Content = JsonContent.Create(BuildCreateTeamRequest("Equipe Alfa"));

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Equipe Alfa", body.GetProperty("name").GetString());
        Assert.Equal(projectId, body.GetProperty("projectId").GetString());

        using var db = factory.CreateConstructionDbContext();
        var persisted = db.Teams.First(t => t.Id == Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.Equal(5, persisted.MemberCount);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenMemberCountIsBelowMinimum()
    {
        // Arrange — regra de domínio (Team.Create: MinMemberCount = 1) deve surgir como 400
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams");
        request.Content = JsonContent.Create(new { name = "Equipe Vazia", specialty = "Pintura", memberCount = 0 });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{projectId}/teams
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProject_ShouldReturn200WithCreatedTeam_WhenCalledByViewer()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams");
        createRequest.Content = JsonContent.Create(BuildCreateTeamRequest("Equipe Visível"));
        await _client.SendAsync(createRequest);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/teams", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var teams = body.EnumerateArray().ToList();
        Assert.Contains(teams, t => t.GetProperty("name").GetString() == "Equipe Visível");
    }

    // -------------------------------------------------------------------------
    // PUT /api/teams/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn404_WhenTeamDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Put, $"/api/teams/{Guid.NewGuid()}");
        request.Content = JsonContent.Create(BuildCreateTeamRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams");
        createRequest.Content = JsonContent.Create(BuildCreateTeamRequest("Nome Original"));
        var createResponse = await _client.SendAsync(createRequest);
        var teamId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var updateRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/teams/{teamId}");
        updateRequest.Content = JsonContent.Create(new { name = "Nome Atualizado", specialty = "Hidráulica", memberCount = 10 });

        // Act
        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var updated = db.Teams.First(t => t.Id == Guid.Parse(teamId!));
        Assert.Equal("Nome Atualizado", updated.Name);
        Assert.Equal(10, updated.MemberCount);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/teams/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_ShouldReturn404_WhenTeamDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Delete, $"/api/teams/{Guid.NewGuid()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldSetStatusToRemovedInDatabase_AndExcludeFromFutureGetByProject()
    {
        // Arrange — TeamRepository.GetByProjectIdAsync filtra Status == Active; a remoção
        // é lógica (soft delete), então a prova real é a EXCLUSÃO da listagem, não a ausência
        // de linha no banco
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams");
        createRequest.Content = JsonContent.Create(BuildCreateTeamRequest("Equipe A Remover"));
        var createResponse = await _client.SendAsync(createRequest);
        var teamId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var deleteRequest = await AuthorizedRequest(HttpMethod.Delete, $"/api/teams/{teamId}");

        // Act
        var deleteResponse = await _client.SendAsync(deleteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var removed = db.Teams.First(t => t.Id == Guid.Parse(teamId!));
        Assert.Equal(Construcheck.Construction.Domain.Teams.TeamStatus.Removed, removed.Status);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/teams", asAdmin: false);
        var getResponse = await _client.SendAsync(getRequest);
        var teams = (await ReadJson(getResponse)).EnumerateArray().ToList();
        Assert.DoesNotContain(teams, t => t.GetProperty("id").GetString() == teamId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
