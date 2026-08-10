using Construcheck.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Construction;

public class BudgetEndpointsTests(CustomWebApplicationFactory factory)
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

    private static object BuildCreateItemRequest(string costCenter = "Fundação", decimal quantity = 10m, decimal unitPrice = 50m) => new
    {
        costCenter,
        description = "Concreto usinado",
        unit = "m³",
        quantity,
        unitPrice,
        sinapiCode = (string?)null
    };

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/budget/items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/budget/items", BuildCreateItemRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenCalledByViewer()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/budget/items", asAdmin: false);
        request.Content = JsonContent.Create(BuildCreateItemRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenProjectDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{Guid.NewGuid()}/budget/items");
        request.Content = JsonContent.Create(BuildCreateItemRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn200WithCorrectTotalValue_WhenDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        request.Content = JsonContent.Create(BuildCreateItemRequest(quantity: 10m, unitPrice: 50m));

        // Act
        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        // Assert — TotalValue é calculado (Quantity * UnitPrice), não um campo persistido direto
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(500m, body.GetProperty("totalValue").GetDecimal());
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenQuantityIsZeroOrNegative()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        request.Content = JsonContent.Create(BuildCreateItemRequest(quantity: 0m));

        var response = await _client.SendAsync(request);
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("A quantidade deve ser maior que zero.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Create_ShouldReturn200_WhenUnitPriceIsExactlyZero()
    {
        // Arrange — fronteira: Money.CreateNonNegative permite zero (item de cortesia/doação),
        // diferente de Contract.Value que exige estritamente positivo
        var projectId = await CreateProjectAsync();
        var request = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        request.Content = JsonContent.Create(BuildCreateItemRequest(unitPrice: 0m));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{projectId}/budget — verifica a estrutura agregada via HTTP real
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProject_ShouldReturnCorrectAggregateShape_WithMultipleCostCenters()
    {
        // Arrange — dois centros de custo distintos; a matemática de agrupamento já foi
        // provada em BudgetApplicationServiceTests (unitário) — aqui o objetivo é confirmar
        // que BudgetSummaryResponse (Items + TotalsByCostCenter + ProjectTotalValue)
        // sobrevive à serialização JSON e ao roteamento HTTP com a forma correta.
        var projectId = await CreateProjectAsync();

        var item1 = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        item1.Content = JsonContent.Create(BuildCreateItemRequest("Fundação", 10m, 50m)); // 500
        await _client.SendAsync(item1);

        var item2 = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        item2.Content = JsonContent.Create(BuildCreateItemRequest("Elétrica", 20m, 15m)); // 300
        await _client.SendAsync(item2);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/budget", asAdmin: false);

        // Act
        var response = await _client.SendAsync(getRequest);
        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.True(body.TryGetProperty("totalsByCostCenter", out var totals));
        Assert.True(body.TryGetProperty("projectTotalValue", out var total));

        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(2, totals.GetArrayLength());
        Assert.Equal(800m, total.GetDecimal());
    }

    // -------------------------------------------------------------------------
    // PUT /api/budget/items/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn404_WhenItemDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Put, $"/api/budget/items/{Guid.NewGuid()}");
        request.Content = JsonContent.Create(BuildCreateItemRequest());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistChanges_WhenDataIsValid()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        createRequest.Content = JsonContent.Create(BuildCreateItemRequest("Centro Original"));
        var createResponse = await _client.SendAsync(createRequest);
        var itemId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var updateRequest = await AuthorizedRequest(HttpMethod.Put, $"/api/budget/items/{itemId}");
        updateRequest.Content = JsonContent.Create(BuildCreateItemRequest("Centro Atualizado", 5m, 100m));

        // Act
        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateConstructionDbContext();
        var updated = db.BudgetItems.First(i => i.Id == Guid.Parse(itemId!));
        Assert.Equal("Centro Atualizado", updated.CostCenter);
        Assert.Equal(500m, updated.TotalValue.Amount);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/budget/items/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_ShouldReturn404_WhenItemDoesNotExist()
    {
        var request = await AuthorizedRequest(HttpMethod.Delete, $"/api/budget/items/{Guid.NewGuid()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldExcludeItemFromFutureProjectSummary()
    {
        // Arrange
        var projectId = await CreateProjectAsync();
        var createRequest = await AuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/budget/items");
        createRequest.Content = JsonContent.Create(BuildCreateItemRequest());
        var createResponse = await _client.SendAsync(createRequest);
        var itemId = (await ReadJson(createResponse)).GetProperty("id").GetString();

        var deleteRequest = await AuthorizedRequest(HttpMethod.Delete, $"/api/budget/items/{itemId}");

        // Act
        var deleteResponse = await _client.SendAsync(deleteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getRequest = await AuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/budget", asAdmin: false);
        var getResponse = await _client.SendAsync(getRequest);
        var body = await ReadJson(getResponse);

        Assert.Equal(0m, body.GetProperty("projectTotalValue").GetDecimal());
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
