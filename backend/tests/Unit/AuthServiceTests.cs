using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Enums;
using Construcheck.API.Modules.Auth.Interfaces;
using Construcheck.API.Modules.Auth.Services;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Auth.Services;

public class AuthServiceTests
{
    private readonly IAuthRepository _repository;
    private readonly ITokenService _tokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _repository = Substitute.For<IAuthRepository>();
        _tokenService = Substitute.For<ITokenService>();
        _sut = new AuthService(_repository, _tokenService);
    }

    // -------------------------------------------------------------------------
    // RegisterAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterUserRequest("user@test.com", "Password123!");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(new User { Email = request.Email });

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Conflict, result.ErrorType);
        Assert.Equal("E-mail já cadastrado.", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnFailure_WhenViewerRoleNotFound()
    {
        // Arrange
        var request = new RegisterUserRequest("new@test.com", "Password123!");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns((Role?)null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Failure, result.ErrorType);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WithViewerRole()
    {
        // Arrange
        var request = new RegisterUserRequest("new@test.com", "Password123!");
        var viewerRole = new Role { Id = Guid.NewGuid(), Name = "Viewer" };

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? createdUser = null;
        await _repository.AddUserAsync(
            Arg.Do<User>(u => createdUser = u),
            Arg.Any<CancellationToken>());

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(createdUser);
        Assert.Equal("new@test.com", createdUser.Email);
        Assert.Single(createdUser.UserRoles);
        Assert.Equal(viewerRole.Id, createdUser.UserRoles.First().RoleId);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ShouldNormalizeEmail_ToLowercase()
    {
        // Arrange
        var request = new RegisterUserRequest("  USER@TEST.COM  ", "Password123!");
        var viewerRole = new Role { Id = Guid.NewGuid(), Name = "Viewer" };

        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? createdUser = null;
        await _repository.AddUserAsync(
            Arg.Do<User>(u => createdUser = u),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        Assert.Equal("user@test.com", createdUser!.Email);
    }

    // -------------------------------------------------------------------------
    // LoginAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_ShouldReturnUnauthorized_WhenEmailNotFound()
    {
        // Arrange
        var request = new LoginUserRequest("notfound@test.com", "any");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
        Assert.Equal("E-mail ou senha inválidos.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnUnauthorized_WhenPasswordIsWrong()
    {
        // Arrange
        var correctPassword = "Password123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword)
        };

        var request = new LoginUserRequest(user.Email, "WrongPassword!");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(user);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var password = "Password123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            UserRoles = []
        };

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "generated-refresh-token",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var request = new LoginUserRequest(user.Email, password);
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(user);
        _tokenService.GenerateRefreshToken(user.Id).Returns(refreshToken);
        _tokenService.GenerateAccessToken(user).Returns("generated-access-token");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("generated-access-token", result.Value!.AccessToken);
        Assert.Equal("generated-refresh-token", result.Value.RefreshToken);
        await _repository.Received(1).AddRefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RefreshAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_ShouldReturnUnauthorized_WhenTokenNotFound()
    {
        // Arrange
        _repository.GetRefreshTokenAsync("invalid-token", Arg.Any<CancellationToken>())
                   .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.RefreshAsync("invalid-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnUnauthorized_WhenTokenIsRevoked()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Token = "revoked-token",
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = new User { UserRoles = [] }
        };

        _repository.GetRefreshTokenAsync("revoked-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.RefreshAsync("revoked-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnUnauthorized_WhenTokenIsExpired()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Token = "expired-token",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // expirado
            User = new User { UserRoles = [] }
        };

        _repository.GetRefreshTokenAsync("expired-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.RefreshAsync("expired-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRotateToken_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@test.com", UserRoles = [] };

        var storedToken = new RefreshToken
        {
            Token = "valid-token",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            UserId = userId,
            User = user
        };

        var newRefreshToken = new RefreshToken
        {
            Token = "new-refresh-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _repository.GetRefreshTokenAsync("valid-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);
        _tokenService.GenerateRefreshToken(userId).Returns(newRefreshToken);
        _tokenService.GenerateAccessToken(user).Returns("new-access-token");

        // Act
        var result = await _sut.RefreshAsync("valid-token");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value!.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);

        // Verifica rotação: token antigo revogado, novo adicionado
        await _repository.Received(1).RevokeRefreshTokenAsync(storedToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).AddRefreshTokenAsync(newRefreshToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // LogoutAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_ShouldReturnUnauthorized_WhenTokenNotFound()
    {
        // Arrange
        _repository.GetRefreshTokenAsync("invalid-token", Arg.Any<CancellationToken>())
                   .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.LogoutAsync("invalid-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task LogoutAsync_ShouldReturnUnauthorized_WhenTokenAlreadyRevoked()
    {
        // Arrange
        var storedToken = new RefreshToken { Token = "token", IsRevoked = true };
        _repository.GetRefreshTokenAsync("token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.LogoutAsync("token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeToken_WhenTokenIsValid()
    {
        // Arrange
        var storedToken = new RefreshToken { Token = "active-token", IsRevoked = false };
        _repository.GetRefreshTokenAsync("active-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.LogoutAsync("active-token");

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).RevokeRefreshTokenAsync(storedToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // UpdateUserRolesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRolesRequest([RoleType.Admin]);
        _repository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
                   .Returns((User?)null);

        // Act
        var result = await _sut.UpdateUserRolesAsync(userId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldReturnValidation_WhenRolesListIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var request = new UpdateUserRolesRequest([]);
        _repository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
                   .Returns(user);

        // Act
        var result = await _sut.UpdateUserRolesAsync(userId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldReturnValidation_WhenRoleDoesNotExistInDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var request = new UpdateUserRolesRequest([RoleType.Admin]);

        _repository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
                   .Returns(user);
        // Retorna lista vazia — role não encontrada no banco
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([]);

        // Act
        var result = await _sut.UpdateUserRolesAsync(userId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldUpdateRoles_WhenDataIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        var request = new UpdateUserRolesRequest([RoleType.Admin]);

        _repository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
                   .Returns(user);
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([adminRole]);

        // Act
        var result = await _sut.UpdateUserRolesAsync(userId, request);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).UpdateUserRolesAsync(
            user,
            Arg.Is<List<Role>>(l => l.Contains(adminRole)),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
