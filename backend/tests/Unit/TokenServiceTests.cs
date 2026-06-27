using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Services;
using Microsoft.Extensions.Configuration;

namespace Construcheck.Unit.Tests.Auth.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut;

    private const string Secret = "construcheck-super-secret-key-for-tests-with-256-bits!!";
    private const string Issuer = "construcheck-test";
    private const string Audience = "construcheck-test";
    private const string ExpirationMinutes = "15";
    private const string RefreshExpirationDays = "7";

    public TokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = Secret,
                ["JWT_ISSUER"] = Issuer,
                ["JWT_AUDIENCE"] = Audience,
                ["JWT_EXPIRATION_MINUTES"] = ExpirationMinutes,
                ["REFRESH_TOKEN_EXPIRATION_DAYS"] = RefreshExpirationDays,
            })
            .Build();

        _sut = new TokenService(configuration);
    }

    // -------------------------------------------------------------------------
    // GenerateAccessToken
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainSubClaimWithUserId()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = ReadToken(token);

        // Assert
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(sub);
        Assert.Equal(user.Id.ToString(), sub.Value);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUserEmail()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = ReadToken(token);

        // Assert
        var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(email);
        Assert.Equal(user.Email, email.Value);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainRolesAsClaims()
    {
        // Arrange
        var user = BuildUserWithTwoRoles("Admin", "Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = ReadToken(token);

        // Assert
        var roles = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Assert.Contains("Admin", roles);
        Assert.Contains("Viewer", roles);
    }

    [Fact]
    public void GenerateAccessToken_ShouldUseConfiguredIssuerAndAudience()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = ReadToken(token);

        // Assert
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_ShouldExpireAtConfiguredTime()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");
        var minutes = int.Parse(ExpirationMinutes);
        var lowerBound = DateTime.UtcNow.AddMinutes(minutes - 1);
        var upperBound = DateTime.UtcNow.AddMinutes(minutes + 1);

        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwt = ReadToken(token);

        // Assert
        Assert.True(jwt.ValidTo > lowerBound);
        Assert.True(jwt.ValidTo < upperBound);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUniqueJti()
    {
        // Arrange
        var user = BuildUserWithRole("Viewer");

        // Act
        var token1 = _sut.GenerateAccessToken(user);
        var token2 = _sut.GenerateAccessToken(user);
        var jti1 = ReadToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = ReadToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        // Assert
        Assert.NotEqual(jti1, jti2);
    }

    // -------------------------------------------------------------------------
    // GenerateRefreshToken
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateRefreshToken_ShouldContainCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateRefreshToken(userId);

        // Assert
        Assert.Equal(userId, token.UserId);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldNotBeRevoked()
    {
        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldExpireAtConfiguredDay()
    {
        // Arrange
        var days = int.Parse(RefreshExpirationDays);
        var expected = DateTime.UtcNow.AddDays(days);

        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert — tolerância de 5 segundos para variação de clock
        Assert.True(token.ExpiresAt > expected.AddSeconds(-5));
        Assert.True(token.ExpiresAt < expected.AddSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_ShouldGenerateUniqueTokens()
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
    public void GenerateRefreshToken_ShouldGenerateValidBase64Token()
    {
        // Act
        var token = _sut.GenerateRefreshToken(Guid.NewGuid());

        // Assert — token é base64 válido com 64 bytes (88 chars base64)
        var bytes = Convert.FromBase64String(token.Token);
        Assert.Equal(64, bytes.Length);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static User BuildUserWithRole(string roleName) => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@test.com",
        UserRoles =
        [
            new UserRole { Role = new Role { Name = roleName } }
        ]
    };

    private static User BuildUserWithTwoRoles(string role1, string role2) => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@test.com",
        UserRoles =
        [
            new UserRole { Role = new Role { Name = role1 } },
            new UserRole { Role = new Role { Name = role2 } }
        ]
    };

    private static JwtSecurityToken ReadToken(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);
}
