using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Services;
using Microsoft.Extensions.Configuration;

namespace Construcheck.Unit.Tests.Auth.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut;
    private readonly IConfiguration _configuration;

    private const string Secret = "construcheck-super-secret-key-para-testes-com-256-bits!!";
    private const string Issuer = "construcheck-test";
    private const string Audience = "construcheck-test";
    private const string ExpirationMinutes = "15";
    private const string RefreshExpirationDays = "7";

    public TokenServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = Secret,
                ["JWT_ISSUER"] = Issuer,
                ["JWT_AUDIENCE"] = Audience,
                ["JWT_EXPIRATION_MINUTES"] = ExpirationMinutes,
                ["REFRESH_TOKEN_EXPIRATION_DAYS"] = RefreshExpirationDays,
            })
            .Build();

        _sut = new TokenService(_configuration);
    }

    // -------------------------------------------------------------------------
    // GenerateAccessToken
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateAccessToken_DeveRetornarTokenValido()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void GenerateAccessToken_DeveConterSubComIdDoUsuario()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = LerToken(token);

        // Assert
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(sub);
        Assert.Equal(user.Id.ToString(), sub.Value);
    }

    [Fact]
    public void GenerateAccessToken_DeveConterEmailDoUsuario()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = LerToken(token);

        // Assert
        var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(email);
        Assert.Equal(user.Email, email.Value);
    }

    [Fact]
    public void GenerateAccessToken_DeveConterRolesComoClaimsDoUsuario()
    {
        // Arrange
        var user = CriarUsuarioComDuasRoles("Admin", "Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = LerToken(token);

        // Assert
        var roles = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Assert.Contains("Admin", roles);
        Assert.Contains("Viewer", roles);
    }

    [Fact]
    public void GenerateAccessToken_DeveUsarIssuerEAudienceConfigurados()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = LerToken(token);

        // Assert
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_DeveExpirarNoTempoConfigurado()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");
        var antes = DateTime.UtcNow.AddMinutes(int.Parse(ExpirationMinutes) - 1);
        var depois = DateTime.UtcNow.AddMinutes(int.Parse(ExpirationMinutes) + 1);

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = LerToken(token);

        // Assert
        Assert.True(jwt.ValidTo > antes);
        Assert.True(jwt.ValidTo < depois);
    }

    [Fact]
    public void GenerateAccessToken_DeveConterJtiUnico()
    {
        // Arrange
        var user = CriarUsuarioComRole("Viewer");

        // Act
        var token1 = _sut.GenerateAccessToken(user);
        var token2 = _sut.GenerateAccessToken(user);
        var jti1 = LerToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = LerToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        // Assert
        Assert.NotEqual(jti1, jti2);
    }

    // -------------------------------------------------------------------------
    // GenerateRefreshToken
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateRefreshToken_DeveRetornarTokenComUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateRefreshToken(userId);

        // Assert
        Assert.Equal(userId, token.UserId);
    }

    [Fact]
    public void GenerateRefreshToken_DeveRetornarTokenNaoRevogado()
    {
        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void GenerateRefreshToken_DeveExpirarNoDiaConfigurado()
    {
        // Arrange
        var dias = int.Parse(RefreshExpirationDays);
        var esperado = DateTime.UtcNow.AddDays(dias);

        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert — tolerância de 5 segundos para variação de clock
        Assert.True(token.ExpiresAt > esperado.AddSeconds(-5));
        Assert.True(token.ExpiresAt < esperado.AddSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_DeveGerarTokensUnicos()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token1 = _sut.GenerateRefreshToken(userId);
        var token2 = _sut.GenerateRefreshToken(userId);

        // Assert
        Assert.NotEqual(token1.Token, token2.Token);
    }

    [Fact]
    public void GenerateRefreshToken_DeveGerarTokenBase64Valido()
    {
        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert — token é base64 válido e tem comprimento esperado (64 bytes → 88 chars base64)
        var bytes = Convert.FromBase64String(token.Token);
        Assert.Equal(64, bytes.Length);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static User CriarUsuarioComRole(string roleName) => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@test.com",
        UserRoles =
        [
            new UserRole
            {
                Role = new Role { Name = roleName }
            }
        ]
    };

    private static User CriarUsuarioComDuasRoles(string role1, string role2) => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@test.com",
        UserRoles =
        [
            new UserRole { Role = new Role { Name = role1 } },
            new UserRole { Role = new Role { Name = role2 } }
        ]
    };

    private static JwtSecurityToken LerToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ReadJwtToken(token);
    }
}
