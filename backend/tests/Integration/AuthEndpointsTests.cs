using Construcheck.Auth.Domain;
using Construcheck.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Auth;

public class AuthEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /api/auth/register
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_ShouldReturn201_WhenDataIsValid()
    {
        var request = new { email = $"new-{Guid.NewGuid()}@test.com", password = "Password123!" };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldReturn400_WhenPasswordIsWeak()
    {
        var request = new { email = $"weak-pass-{Guid.NewGuid()}@test.com", password = "weak" };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldReturn409_WhenEmailAlreadyExists()
    {
        var email = $"duplicate-{Guid.NewGuid()}@test.com";
        var request = new { email, password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldNotReturnToken_InResponseBody()
    {
        var request = new { email = $"no-token-{Guid.NewGuid()}@test.com", password = "Password123!" };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Register_ShouldCreateUserWithViewerRole_InDatabase()
    {
        var email = $"viewer-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        using var db = factory.CreateAuthDbContext();
        var user = db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsEnumerable()
            .Where(u => u.Email.Value == email)
            .Select(u => new
            {
                Email = u.Email.Value,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
            })
            .FirstOrDefault();

        Assert.NotNull(user);
        Assert.Single(user.Roles);
        Assert.Contains("Viewer", user.Roles);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_ShouldReturn200WithAccessToken_WhenCredentialsAreValid()
    {
        var email = $"login-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("accessToken", out var token));
        Assert.NotEmpty(token.GetString()!);
    }

    [Fact]
    public async Task Login_ShouldSetHttpOnlyCookie_WhenCredentialsAreValid()
    {
        var email = $"cookie-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var setCookie = response.Headers
            .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value;

        Assert.NotNull(setCookie);
        var cookieHeader = setCookie.First();
        Assert.Contains("refreshToken=", cookieHeader);
        Assert.Contains("httponly", cookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenEmailDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "notfound@test.com", password = "any" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenPasswordIsWrong()
    {
        var email = $"wrong-pass-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "CorrectPassword123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/refresh
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_ShouldReturn200WithNewAccessToken_WhenCookieIsValid()
    {
        // Arrange
        var email = $"refresh-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var cookieValue = ExtractRefreshTokenCookie(loginResponse);
        Assert.NotNull(cookieValue);

        // Act
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookieValue);
        var response = await _client.SendAsync(refreshRequest);

        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("accessToken", out var newToken));
        Assert.NotEmpty(newToken.GetString()!);
    }

    [Fact]
    public async Task Refresh_ShouldReturn401_WhenNoCookieIsPresent()
    {
        var response = await _client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldRotateToken_InDatabaseAfterUse()
    {
        // Arrange
        var email = $"rotation-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var cookieValue = ExtractRefreshTokenCookie(loginResponse);
        Assert.NotNull(cookieValue);

        // Act — primeiro refresh
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookieValue);
        await _client.SendAsync(refreshRequest);

        // Assert — token original revogado, novo ativo (RefreshToken agora é lido
        // via o User dono, não como tabela solta — carregamento explícito necessário)
        using var db = factory.CreateAuthDbContext();
        var user = db.Users
            .Include(u => u.RefreshTokens)
            .AsEnumerable()
            .First(u => u.Email.Value == email);

        Assert.True(user.RefreshTokens.Count >= 2);
        Assert.Contains(user.RefreshTokens, t => t.IsRevoked);
        Assert.Contains(user.RefreshTokens, t => !t.IsRevoked);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/logout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_ShouldReturn204_WhenAuthenticated()
    {
        // Arrange
        var email = $"logout-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var cookieValue = ExtractRefreshTokenCookie(loginResponse);
        Assert.NotNull(cookieValue);

        // Act
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookieValue);
        var response = await _client.SendAsync(logoutRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldReturn204_WhenNoCookieIsPresent()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldRevokeToken_InDatabase()
    {
        // Arrange
        var email = $"logout-db-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var cookieValue = ExtractRefreshTokenCookie(loginResponse);
        Assert.NotNull(cookieValue);

        // Act
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookieValue);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Assert
        using var db = factory.CreateAuthDbContext();
        var user = db.Users
            .Include(u => u.RefreshTokens)
            .AsEnumerable()
            .First(u => u.Email.Value == email);

        Assert.True(user.RefreshTokens.First().IsRevoked);
    }

    [Fact]
    public async Task Logout_ShouldMakeRefreshFail_AfterRevocation()
    {
        // Arrange
        var email = $"logout-refresh-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var cookieValue = ExtractRefreshTokenCookie(loginResponse);
        Assert.NotNull(cookieValue);

        // Logout
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookieValue);
        await _client.SendAsync(logoutRequest);

        // Act — tenta refresh com o mesmo cookie após logout
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookieValue);
        var response = await _client.SendAsync(refreshRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/auth/users/{id}/roles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateRoles_ShouldReturn401_WhenNoTokenProvided()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/auth/users/{Guid.NewGuid()}/roles",
            new { roles = new[] { "Admin" } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn403_WhenCalledByViewer()
    {
        // Arrange
        var email = $"viewer-roles-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");
        var accessToken = await GetAccessToken(email, "Password123!");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{Guid.NewGuid()}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { "Admin" } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn404_WhenUserDoesNotExist()
    {
        // Arrange
        var adminToken = await CreateAdminAndGetToken();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{Guid.NewGuid()}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { 1 } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn200_WhenAdminUpdatesAnotherUsersRoles()
    {
        // Arrange
        var adminToken = await CreateAdminAndGetToken();

        var targetEmail = $"target-{Guid.NewGuid()}@test.com";
        await RegisterUser(targetEmail, "Password123!");

        using var db = factory.CreateAuthDbContext();
        var targetUserId = db.Users.AsEnumerable().First(u => u.Email.Value == targetEmail).Id;

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{targetUserId}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { 0 } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db2 = factory.CreateAuthDbContext();
        var currentRoles = db2.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsEnumerable()
            .First(u => u.Id == targetUserId)
            .UserRoles
            .Select(ur => ur.Role.Name)
            .ToList();

        Assert.Contains("Admin", currentRoles);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task RegisterUser(string email, string password) =>
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

    private async Task<string> GetAccessToken(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await ReadJson(response);
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<string> CreateAdminAndGetToken()
    {
        var email = $"admin-{Guid.NewGuid()}@test.com";
        const string password = "Admin123!";

        await RegisterUser(email, password);

        // Promove para Admin diretamente no banco
        using var db = factory.CreateAuthDbContext();
        var user = db.Users
            .Include(u => u.UserRoles)
            .AsEnumerable()
            .First(u => u.Email.Value == email);
        var adminRole = db.Roles.First(r => r.Name == "Admin");

        var replaceResult = user.ReplaceRoles([adminRole]);
        if (replaceResult.IsFailure)
            throw new InvalidOperationException($"Falha ao promover usuário de teste a Admin: {replaceResult.Error}");

        db.SaveChanges();

        return await GetAccessToken(email, password);
    }

    /// <summary>
    /// Extrai o valor do cookie refreshToken do header Set-Cookie da resposta.
    /// Retorna apenas "refreshToken=valor" sem os atributos (HttpOnly, Path, etc).
    /// </summary>
    private static string? ExtractRefreshTokenCookie(HttpResponseMessage response)
    {
        var setCookieHeader = response.Headers
            .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value?.First();

        return setCookieHeader?.Split(';')[0]; // "refreshToken=xyz"
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
