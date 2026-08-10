using Construcheck.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Construction;

public class ContractsEndpointsTests(CustomWebApplicationFactory factory)
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

    private static object BuildCreateContractRequest(decimal value = 50000m) => new
    {
        type = 0, // ContractType.Contractor
        counterpartyName = "Empreiteira ABC",
        value,
        startDate = "2026-01-01",
        dueDate = "2026-06-01",
        responsible = "Fulano"
    };

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/contracts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/contracts", BuildCreateContractRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenCalledByViewer()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/contracts", asAdmin: false);
        request.Content = JsonContent.Create(BuildCreateContractRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/contracts");
        request.Content = JsonContent.Create(BuildCreateContractRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn200AndPersist_WhenProjectExistsAndDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        request.Content = JsonContent.Create(BuildCreateContractRequest(75000m));

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(75000m, body.GetProperty("value").GetDecimal());

        using var db = factory.CreateConstructionDbContext();
        var persisted = db.Contracts.First(c => c.Id == Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.Equal("Empreiteira ABC", persisted.CounterpartyName);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenValueIsZeroOrNegative()
    {
        // Arrange — Money.CreatePositive recusa valor <= 0
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        request.Content = JsonContent.Create(BuildCreateContractRequest(0m));

        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("O valor deve ser maior que zero.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenDueDateIsBeforeStartDate()
    {
        // Arrange — segunda regra de domínio distinta (DateRange.Create), não Money
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        request.Content = JsonContent.Create(new
        {
            type = 0,
            counterpartyName = "Empreiteira",
            value = 1000m,
            startDate = "2026-06-01",
            dueDate = "2026-01-01",
            responsible = "Fulano"
        });

        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("A data final não pode ser anterior à data inicial.", body.GetProperty("error").GetString());
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{projectId}/contracts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProject_ShouldReturn200WithCreatedContract_WhenCalledByViewer()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        createRequest.Content = JsonContent.Create(BuildCreateContractRequest());
        await _client.SendAsync(createRequest);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/contracts", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(body.EnumerateArray());
    }

    // -------------------------------------------------------------------------
    // GET /api/contracts/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_ShouldReturn404_WhenContractDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Get, $"/api/contracts/{Guid.NewGuid()}", asAdmin: false);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/contracts/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn404_WhenContractDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Put, $"/api/contracts/{Guid.NewGuid()}");
        request.Content = JsonContent.Create(BuildCreateContractRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        createRequest.Content = JsonContent.Create(BuildCreateContractRequest(1000m));
        var createResponse = await _client.SendAsync(createRequest);
        var contractId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var updateRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/contracts/{contractId}");
        updateRequest.Content = JsonContent.Create(BuildCreateContractRequest(2000m));

        // Act
        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var updated = db.Contracts.First(c => c.Id == Guid.Parse(contractId!));
        Assert.Equal(2000m, updated.Value.Amount);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/contracts/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_ShouldReturn404_WhenContractDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Delete, $"/api/contracts/{Guid.NewGuid()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldSetStatusToRemovedInDatabase()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/contracts");
        createRequest.Content = JsonContent.Create(BuildCreateContractRequest());
        var createResponse = await _client.SendAsync(createRequest);
        var contractId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var deleteRequest = await AuthorizedRequest(HttpMethod.Delete, $"/api/contracts/{contractId}");

        // Act
        var response = await _client.SendAsync(deleteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var removed = db.Contracts.First(c => c.Id == Guid.Parse(contractId!));
        Assert.Equal(Construcheck.Construction.Domain.Contracts.ContractStatus.Removed, removed.Status);
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
