using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Construcheck.API.Data;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Construcheck.Integration.Tests.Auth;

public class AuthEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /api/auth/register
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_DeveRetornar201_QuandoDadosValidos()
    {
        // Arrange
        var request = new { email = $"novo-{Guid.NewGuid()}@test.com", password = "Senha123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_DeveRetornar409_QuandoEmailJaCadastrado()
    {
        // Arrange — registra o usuário primeiro
        var email = $"duplicado-{Guid.NewGuid()}@test.com";
        var request = new { email, password = "Senha123!" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act — tenta registrar novamente com o mesmo e-mail
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_NaoDeveRetornarToken_NaResposta()
    {
        // Arrange
        var request = new { email = $"sem-token-{Guid.NewGuid()}@test.com", password = "Senha123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(body); // 201 Created sem body
    }

    [Fact]
    public async Task Register_DeveCriarUsuarioComRoleViewer_NoBanco()
    {
        // Arrange
        var email = $"viewer-{Guid.NewGuid()}@test.com";
        var request = new { email, password = "Senha123!" };

        // Act
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert — verifica diretamente no banco
        using var db = factory.CriarDbContext();
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
    public async Task Login_DeveRetornar200ComAccessToken_QuandoCredenciaisValidas()
    {
        // Arrange
        var email = $"login-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        var body = await LerJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("accessToken", out var token));
        Assert.NotEmpty(token.GetString()!);
    }

    [Fact]
    public async Task Login_DeveDefinirCookieHttpOnly_QuandoCredenciaisValidas()
    {
        // Arrange
        var email = $"cookie-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

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
    public async Task Login_DeveRetornar401_QuandoEmailNaoExiste()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "naoexiste@test.com", password = "qualquer" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DeveRetornar401_QuandoSenhaErrada()
    {
        // Arrange
        var email = $"senha-errada-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "SenhaCorreta123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "SenhaErrada!" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/refresh
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_DeveRetornar200ComNovoAccessToken_QuandoCookieValido()
    {
        // Arrange — login para obter o cookie
        var email = $"refresh-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        var clientComCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientComCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        // Act
        var response = await clientComCookies.PostAsync("/api/auth/refresh", null);
        var body = await LerJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("accessToken", out var novoToken));
        Assert.NotEmpty(novoToken.GetString()!);
    }

    [Fact]
    public async Task Refresh_DeveRetornar401_QuandoSemCookie()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_DeveRotacionarToken_NaoBancoAposUso()
    {
        // Arrange
        var email = $"rotacao-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        var clientComCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientComCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        // Act — primeiro refresh
        await clientComCookies.PostAsync("/api/auth/refresh", null);

        // Assert — verifica no banco que o token original está revogado
        using var db = factory.CriarDbContext();
        var user = db.Users.First(u => u.Email == email);
        var tokens = db.RefreshTokens.Where(t => t.UserId == user.Id).ToList();

        Assert.True(tokens.Count >= 2); // original + novo
        Assert.Contains(tokens, t => t.IsRevoked);
        Assert.Contains(tokens, t => !t.IsRevoked);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/logout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_DeveRetornar204_QuandoAutenticado()
    {
        // Arrange
        var email = $"logout-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        var clientComCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientComCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        // Act
        var response = await clientComCookies.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeveRetornar204_QuandoSemCookie()
    {
        // Logout sem cookie também retorna 204 — cliente já está "fora"
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeveRevogarToken_NoBanco()
    {
        // Arrange
        var email = $"logout-banco-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        var clientComCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientComCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        // Act
        await clientComCookies.PostAsync("/api/auth/logout", null);

        // Assert
        using var db = factory.CriarDbContext();
        var user = db.Users.First(u => u.Email == email);
        var tokens = db.RefreshTokens.Where(t => t.UserId == user.Id).ToList();

        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task Logout_DeveFazerRefreshFalhar_AposRevogacao()
    {
        // Arrange
        var email = $"logout-refresh-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");

        var clientComCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true
            });

        await clientComCookies.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Senha123!" });

        await clientComCookies.PostAsync("/api/auth/logout", null);

        // Act — tenta refresh após logout
        var response = await clientComCookies.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/auth/users/{id}/roles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateRoles_DeveRetornar401_QuandoSemToken()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/auth/users/{Guid.NewGuid()}/roles",
            new { roles = new[] { "Admin" } });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_DeveRetornar403_QuandoTokenDeViewer()
    {
        // Arrange — registra usuário Viewer e faz login
        var email = $"viewer-roles-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(email, "Senha123!");
        var accessToken = await ObterAccessToken(email, "Senha123!");

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
    public async Task UpdateRoles_DeveRetornar404_QuandoUsuarioNaoExiste()
    {
        // Arrange — cria um Admin direto no banco e faz login
        var adminToken = await CriarAdminEObterToken();

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
    public async Task UpdateRoles_DeveRetornar200_QuandoAdminAtualizaRoleDeOutroUsuario()
    {
        // Arrange
        var adminToken = await CriarAdminEObterToken();

        var emailAlvo = $"alvo-{Guid.NewGuid()}@test.com";
        await RegistrarUsuario(emailAlvo, "Senha123!");

        using var db = factory.CriarDbContext();
        var userIdAlvo = db.Users.First(u => u.Email == emailAlvo).Id;

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/auth/users/{userIdAlvo}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { "Admin" } })
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db2 = factory.CriarDbContext();
        var rolesAtuais = db2.UserRoles
            .Where(ur => ur.UserId == userIdAlvo)
            .Select(ur => ur.Role.Name)
            .ToList();

        Assert.Contains("Admin", rolesAtuais);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task RegistrarUsuario(string email, string password)
    {
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
    }

    private async Task<string> ObterAccessToken(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await LerJson(response);
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<string> CriarAdminEObterToken()
    {
        var email = $"admin-{Guid.NewGuid()}@test.com";
        var password = "Admin123!";

        await RegistrarUsuario(email, password);

        // Promove para Admin diretamente no banco
        using var db = factory.CriarDbContext();
        var user = db.Users.First(u => u.Email == email);
        var adminRole = db.Roles.First(r => r.Name == "Admin");
        var viewerRole = db.Roles.First(r => r.Name == "Viewer");

        db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.UserId == user.Id));
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        return await ObterAccessToken(email, password);
    }

    private static async Task<JsonElement> LerJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
