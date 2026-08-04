using Construcheck.Auth.Application.DTOs;
using Construcheck.Auth.Application.Services;
using Construcheck.Auth.Domain;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Auth.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _repository;
    private readonly ITokenService _tokenService;
    private readonly AuthApplicationService _sut;

    public AuthServiceTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _tokenService = Substitute.For<ITokenService>();
        _sut = new AuthApplicationService(_repository, _tokenService);
    }

    // -------------------------------------------------------------------------
    // RegisterAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterUserRequest("user@test.com", "Password123!");
        var existingUser = User.Create("user@test.com", "Password123!").Value!;
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(existingUser);

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
    public async Task RegisterAsync_ShouldReturnValidation_WhenPasswordIsWeak()
    {
        // Arrange — senha sem número, sem maiúscula, sem símbolo, curta demais
        var request = new RegisterUserRequest("new@test.com", "weak");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(Role.Create(Guid.NewGuid(), "Viewer", "Acesso somente leitura."));

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WithViewerRole()
    {
        // Arrange
        var request = new RegisterUserRequest("new@test.com", "Password123!");
        var viewerRole = Role.Create(Guid.NewGuid(), "Viewer", "Acesso somente leitura.");

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? createdUser = null;
        await _repository.AddAsync(
            Arg.Do<User>(u => createdUser = u),
            Arg.Any<CancellationToken>());

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(createdUser);
        Assert.Equal("new@test.com", createdUser.Email.Value);
        Assert.Single(createdUser.UserRoles);
        Assert.Equal(viewerRole.Id, createdUser.UserRoles.First().RoleId);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ShouldNormalizeEmail_ToLowercase()
    {
        // Arrange
        var request = new RegisterUserRequest("  USER@TEST.COM  ", "Password123!");
        var viewerRole = Role.Create(Guid.NewGuid(), "Viewer", "Acesso somente leitura.");

        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? createdUser = null;
        await _repository.AddAsync(
            Arg.Do<User>(u => createdUser = u),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        Assert.Equal("user@test.com", createdUser!.Email.Value);
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
        var user = User.Create("user@test.com", "CorrectPassword123!").Value!;

        var request = new LoginUserRequest("user@test.com", "WrongPassword!");
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
        var user = User.Create("user@test.com", password).Value!;

        var refreshToken = RefreshToken.Create(user.Id, "generated-refresh-token", DateTime.UtcNow.AddDays(7));

        var request = new LoginUserRequest("user@test.com", password);
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
        Assert.Single(user.RefreshTokens);
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
        var storedToken = RefreshToken.Create(Guid.NewGuid(), "revoked-token", DateTime.UtcNow.AddDays(7));
        storedToken.Revoke();

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
        // Arrange — expirado: ExpiresAt no passado
        var storedToken = RefreshToken.Create(Guid.NewGuid(), "expired-token", DateTime.UtcNow.AddDays(-1));

        _repository.GetRefreshTokenAsync("expired-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.RefreshAsync("expired-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnUnauthorized_WhenOwnerUserNotFound()
    {
        // Arrange — token válido, mas o User dono não é encontrado pelo repositório
        // (cenário que só existe agora porque RefreshToken faz parte do Aggregate de User,
        // e é buscado separadamente do token em si — ver decisão registrada na modelagem)
        var userId = Guid.NewGuid();
        var storedToken = RefreshToken.Create(userId, "valid-token", DateTime.UtcNow.AddDays(7));

        _repository.GetRefreshTokenAsync("valid-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
                   .Returns((User?)null);

        // Act
        var result = await _sut.RefreshAsync("valid-token");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRotateToken_WhenTokenIsValid()
    {
        // Arrange
        var user = User.Create("user@test.com", "Password123!").Value!;

        var storedToken = RefreshToken.Create(user.Id, "valid-token", DateTime.UtcNow.AddDays(7));

        var newRefreshToken = RefreshToken.Create(user.Id, "new-refresh-token", DateTime.UtcNow.AddDays(7));

        _repository.GetRefreshTokenAsync("valid-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                   .Returns(user);
        _tokenService.GenerateRefreshToken(user.Id).Returns(newRefreshToken);
        _tokenService.GenerateAccessToken(user).Returns("new-access-token");

        // Act
        var result = await _sut.RefreshAsync("valid-token");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value!.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);

        // Verifica rotação: token antigo revogado, novo anexado ao User
        Assert.True(storedToken.IsRevoked);
        Assert.Contains(user.RefreshTokens, t => t.Token == "new-refresh-token");
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
        var storedToken = RefreshToken.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddDays(7));
        storedToken.Revoke();

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
        var storedToken = RefreshToken.Create(Guid.NewGuid(), "active-token", DateTime.UtcNow.AddDays(7));

        _repository.GetRefreshTokenAsync("active-token", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.LogoutAsync("active-token");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(storedToken.IsRevoked);
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
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
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
        var user = User.Create("user@test.com", "Password123!").Value!;
        var request = new UpdateUserRolesRequest([]);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                   .Returns(user);

        // Act
        var result = await _sut.UpdateUserRolesAsync(user.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldReturnValidation_WhenRoleDoesNotExistInDatabase()
    {
        // Arrange
        var user = User.Create("user@test.com", "Password123!").Value!;
        var request = new UpdateUserRolesRequest([RoleType.Admin]);

        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                   .Returns(user);
        // Retorna lista vazia — role não encontrada no banco
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([]);

        // Act
        var result = await _sut.UpdateUserRolesAsync(user.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_ShouldUpdateRoles_WhenDataIsValid()
    {
        // Arrange
        var user = User.Create("user@test.com", "Password123!").Value!;
        var adminRole = Role.Create(Guid.NewGuid(), "Admin", "Acesso total.");
        var request = new UpdateUserRolesRequest([RoleType.Admin]);

        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                   .Returns(user);
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([adminRole]);

        // Act
        var result = await _sut.UpdateUserRolesAsync(user.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(user.UserRoles);
        Assert.Equal(adminRole.Id, user.UserRoles.First().RoleId);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
