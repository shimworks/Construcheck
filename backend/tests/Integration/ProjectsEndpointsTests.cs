using Construcheck.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Construction;

public class ProjectsEndpointsTests(CustomWebApplicationFactory factory)
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

    private static object BuildCreateRequest(string name = "Edifício Teste") => new
    {
        name,
        address = "Rua Teste, 100",
        technicalManager = "Gestor Teste",
        startDate = "2026-01-01",
        targetEndDate = "2026-12-31"
    };

    // -------------------------------------------------------------------------
    // POST /api/projects
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", BuildCreateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenCalledByViewer()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects", asAdmin: false);
        request.Content = JsonContent.Create(BuildCreateRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn200_WhenCalledByAdminWithValidData()
    {
        // Arrange — confirma explicitamente que é 200 (via Ok()), não 201: Construction
        // não tem o tratamento especial que AuthController.Register tem.
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        request.Content = JsonContent.Create(BuildCreateRequest("Edifício Aurora"));

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Edifício Aurora", body.GetProperty("name").GetString());
        Assert.True(body.TryGetProperty("id", out var id));
        Assert.NotEqual(Guid.Empty, Guid.Parse(id.GetString()!));
    }

    [Fact]
    public async Task Create_ShouldPersistProjectInDatabase()
    {
        // Arrange
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        var uniqueName = $"Obra Persistência {Guid.NewGuid()}";
        request.Content = JsonContent.Create(BuildCreateRequest(uniqueName));

        // Act
        await _client.SendAsync(request);

        // Assert
        using var db = factory.CreateConstructionDbContext();
        var project = db.Projects.AsEnumerable().FirstOrDefault(p => p.Name == uniqueName);
        Assert.NotNull(project);
        Assert.Equal("Rua Teste, 100", project.Address);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenDateRangeIsInvalid()
    {
        // Arrange — endDate antes de startDate: violação de domínio (DateRange.Create),
        // deve surgir como 400 através do ResultExtensions (Validation -> BadRequest)
        var request = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        request.Content = JsonContent.Create(new
        {
            name = "Obra Inválida",
            address = "Endereço",
            technicalManager = "Gestor",
            startDate = "2026-12-31",
            targetEndDate = "2026-01-01"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ShouldReturn200_WhenCalledByViewer()
    {
        // Arrange — GET não tem [Authorize(Roles="Admin")], só [Authorize]: Viewer deve conseguir
        var createRequest = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        createRequest.Content = JsonContent.Create(BuildCreateRequest($"Obra GetAll {Guid.NewGuid()}"));
        await _client.SendAsync(createRequest);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, "/api/projects", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{Guid.NewGuid()}", asAdmin: false);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn200WithCorrectData_WhenProjectExists()
    {
        // Arrange
        var createRequest = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        createRequest.Content = JsonContent.Create(BuildCreateRequest("Obra GetById"));
        var createResponse = await _client.SendAsync(createRequest);
        var createdId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{createdId}", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Obra GetById", body.GetProperty("name").GetString());
    }

    // -------------------------------------------------------------------------
    // PUT /api/projects/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Put, $"/api/projects/{Guid.NewGuid()}");
        request.Content = JsonContent.Create(BuildCreateRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenDataIsValid()
    {
        // Arrange
        var createRequest = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        createRequest.Content = JsonContent.Create(BuildCreateRequest("Nome Original"));
        var createResponse = await _client.SendAsync(createRequest);
        var createdId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var updateRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/projects/{createdId}");
        updateRequest.Content = JsonContent.Create(BuildCreateRequest("Nome Atualizado"));

        // Act
        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var updated = db.Projects.First(p => p.Id == Guid.Parse(createdId!));
        Assert.Equal("Nome Atualizado", updated.Name);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/projects/{id}/arquivar
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Archive_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Patch, $"/api/projects/{Guid.NewGuid()}/arquivar");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_ShouldSetStatusToArchivedInDatabase_WhenProjectExists()
    {
        // Arrange
        var createRequest = await AuthorizedRequest(HttpMethod.Post, "/api/projects");
        createRequest.Content = JsonContent.Create(BuildCreateRequest("Obra Para Arquivar"));
        var createResponse = await _client.SendAsync(createRequest);
        var createdId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var archiveRequest = await AuthorizedRequest(HttpMethod.Patch, $"/api/projects/{createdId}/arquivar");

        // Act
        var response = await _client.SendAsync(archiveRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var archived = db.Projects.First(p => p.Id == Guid.Parse(createdId!));
        Assert.Equal(Construcheck.Construction.Domain.Projects.ProjectStatus.Archived, archived.Status);
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
