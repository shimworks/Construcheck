using Construcheck.API.Data;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        // Arrange
        var request = new { email = $"new-{Guid.NewGuid()}@test.com", password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldReturn409_WhenEmailAlreadyExists()
    {
        // Arrange — registra o usuário primeiro
        var email = $"duplicate-{Guid.NewGuid()}@test.com";
        var request = new { email, password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act — tenta registrar novamente com o mesmo e-mail
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldNotReturnToken_InResponseBody()
    {
        // Arrange
        var request = new { email = $"no-token-{Guid.NewGuid()}@test.com", password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(body); // 201 Created sem body
    }

    [Fact]
    public async Task Register_ShouldCreateUserWithViewerRole_InDatabase()
    {
        // Arrange
        var email = $"viewer-{Guid.NewGuid()}@test.com";
        var request = new { email, password = "Password123!" };

        // Act
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert — verifica diretamente no banco
        using var db = factory.CreateDbContext();
        var user = db.Users
            .Where(u => u.Email == email)
            .Select(u => new
            {
                u.Email,
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
        // Arrange
        var email = $"login-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        var body = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("accessToken", out var token));
        Assert.NotEmpty(token.GetString()!);
    }

    [Fact]
    public async Task Login_ShouldSetHttpOnlyCookie_WhenCredentialsAreValid()
    {
        // Arrange
        var email = $"cookie-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        // Assert
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
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "notfound@test.com", password = "any" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenPasswordIsWrong()
    {
        // Arrange
        var email = $"wrong-pass-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "CorrectPassword123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword!" });

        // Assert
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

        // Extrai o cookie da resposta do login manualmente
        var setCookieHeader = loginResponse.Headers
            .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value?.First();

        Assert.NotNull(setCookieHeader);
        var cookieValue = setCookieHeader.Split(';')[0]; // "refreshToken=xyz"

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
        // Act
        var response = await _client.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldRotateToken_InDatabaseAfterUse()
    {
        // Arrange
        var email = $"rotation-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var clientWithCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientWithCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        // Act — primeiro refresh
        await clientWithCookies.PostAsync("/api/auth/refresh", null);

        // Assert — verifica no banco que o token original está revogado e o novo está ativo
        using var db = factory.CreateDbContext();
        var user = db.Users.First(u => u.Email == email);
        var tokens = db.RefreshTokens.Where(t => t.UserId == user.Id).ToList();

        Assert.True(tokens.Count >= 2);
        Assert.Contains(tokens, t => t.IsRevoked);
        Assert.Contains(tokens, t => !t.IsRevoked);
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

        var clientWithCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientWithCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        // Act
        var response = await clientWithCookies.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldReturn204_WhenNoCookieIsPresent()
    {
        // Logout sem cookie também retorna 204 — cliente já está "fora"
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

        // Extrai o cookie da resposta do login manualmente
        var setCookieHeader = loginResponse.Headers
            .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value?.First();

        Assert.NotNull(setCookieHeader);

        // Extrai apenas o valor do cookie (antes do primeiro ;)
        var cookieValue = setCookieHeader.Split(';')[0]; // "refreshToken=xyz"

        // Act — envia o cookie manualmente no header
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookieValue);
        var logoutResponse = await _client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Assert
        using var db = factory.CreateDbContext();
        var user = db.Users.First(u => u.Email == email);
        var token = db.RefreshTokens
            .AsNoTracking()
            .First(t => t.UserId == user.Id);

        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task Logout_ShouldMakeRefreshFail_AfterRevocation()
    {
        // Arrange
        var email = $"logout-refresh-{Guid.NewGuid()}@test.com";
        await RegisterUser(email, "Password123!");

        var clientWithCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientWithCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password123!" });

        await clientWithCookies.PostAsync("/api/auth/logout", null);

        // Act — tenta refresh após logout
        var response = await clientWithCookies.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/auth/users/{id}/roles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateRoles_ShouldReturn401_WhenNoTokenProvided()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/auth/users/{Guid.NewGuid()}/roles",
            new { roles = new[] { "Admin" } });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn403_WhenCalledByViewer()
    {
        // Arrange — registra usuário Viewer e faz login
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

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn404_WhenUserDoesNotExist()
    {
        // Arrange — cria um Admin direto no banco e faz login
        var adminToken = await CreateAdminAndGetToken();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{Guid.NewGuid()}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { "Viewer" } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ShouldReturn200_WhenAdminUpdatesAnotherUsersRoles()
    {
        // Arrange
        var adminToken = await CreateAdminAndGetToken();

        var targetEmail = $"target-{Guid.NewGuid()}@test.com";
        await RegisterUser(targetEmail, "Password123!");

        using var db = factory.CreateDbContext();
        var targetUserId = db.Users.First(u => u.Email == targetEmail).Id;

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{targetUserId}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { "Admin" } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db2 = factory.CreateDbContext();
        var currentRoles = db2.UserRoles
            .Where(ur => ur.UserId == targetUserId)
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
        using var db = factory.CreateDbContext();
        var user = db.Users.First(u => u.Email == email);
        var adminRole = db.Roles.First(r => r.Name == "Admin");

        db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.UserId == user.Id));
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        return await GetAccessToken(email, password);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
