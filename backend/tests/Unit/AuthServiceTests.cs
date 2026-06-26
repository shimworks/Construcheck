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
    private readonly TokenService _tokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _repository = Substitute.For<IAuthRepository>();
        _tokenService = Substitute.For<TokenService>();
        _sut = new AuthService(_repository, _tokenService);
    }

    // -------------------------------------------------------------------------
    // RegisterAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_DeveRetornarConflict_QuandoEmailJaCadastrado()
    {
        // Arrange
        var request = new RegisterUserRequest("user@test.com", "Senha123!");
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
    public async Task RegisterAsync_DeveRetornarFailure_QuandoRoleViewerNaoEncontrada()
    {
        // Arrange
        var request = new RegisterUserRequest("novo@test.com", "Senha123!");
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
    public async Task RegisterAsync_DeveCriarUsuario_ComRoleViewer()
    {
        // Arrange
        var request = new RegisterUserRequest("novo@test.com", "Senha123!");
        var viewerRole = new Role { Id = Guid.NewGuid(), Name = "Viewer" };

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? usuarioCriado = null;
        await _repository.AddUserAsync(
            Arg.Do<User>(u => usuarioCriado = u),
            Arg.Any<CancellationToken>());

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(usuarioCriado);
        Assert.Equal("novo@test.com", usuarioCriado.Email);
        Assert.Single(usuarioCriado.UserRoles);
        Assert.Equal(viewerRole.Id, usuarioCriado.UserRoles.First().RoleId);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DeveNormalizarEmail_ParaMinusculo()
    {
        // Arrange
        var request = new RegisterUserRequest("  USUARIO@TEST.COM  ", "Senha123!");
        var viewerRole = new Role { Id = Guid.NewGuid(), Name = "Viewer" };

        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        User? usuarioCriado = null;
        await _repository.AddUserAsync(
            Arg.Do<User>(u => usuarioCriado = u),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        Assert.Equal("usuario@test.com", usuarioCriado!.Email);
    }

    // -------------------------------------------------------------------------
    // LoginAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_DeveRetornarUnauthorized_QuandoEmailNaoEncontrado()
    {
        // Arrange
        var request = new LoginUserRequest("naoexiste@test.com", "qualquer");
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
    public async Task LoginAsync_DeveRetornarUnauthorized_QuandoSenhaErrada()
    {
        // Arrange
        var senhaCorreta = "Senha123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(senhaCorreta)
        };

        var request = new LoginUserRequest(user.Email, "SenhaErrada!");
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(user);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarTokens_QuandoCredenciaisValidas()
    {
        // Arrange
        var senha = "Senha123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(senha),
            UserRoles = []
        };

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "refresh-token-gerado",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var request = new LoginUserRequest(user.Email, senha);
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns(user);
        _tokenService.GenerateRefreshToken(user.Id).Returns(refreshToken);
        _tokenService.GenerateAccessToken(user).Returns("access-token-gerado");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("access-token-gerado", result.Value!.AccessToken);
        Assert.Equal("refresh-token-gerado", result.Value.RefreshToken);
        await _repository.Received(1).AddRefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RefreshAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_DeveRetornarUnauthorized_QuandoTokenNaoEncontrado()
    {
        // Arrange
        _repository.GetRefreshTokenAsync("token-invalido", Arg.Any<CancellationToken>())
                   .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.RefreshAsync("token-invalido");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_DeveRetornarUnauthorized_QuandoTokenRevogado()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Token = "token-revogado",
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = new User { UserRoles = [] }
        };

        _repository.GetRefreshTokenAsync("token-revogado", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.RefreshAsync("token-revogado");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_DeveRetornarUnauthorized_QuandoTokenExpirado()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Token = "token-expirado",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // expirado
            User = new User { UserRoles = [] }
        };

        _repository.GetRefreshTokenAsync("token-expirado", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.RefreshAsync("token-expirado");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task RefreshAsync_DeveRotacionarToken_QuandoValido()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@test.com", UserRoles = [] };

        var storedToken = new RefreshToken
        {
            Token = "token-valido",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            UserId = userId,
            User = user
        };

        var novoRefreshToken = new RefreshToken
        {
            Token = "novo-refresh-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _repository.GetRefreshTokenAsync("token-valido", Arg.Any<CancellationToken>())
                   .Returns(storedToken);
        _tokenService.GenerateRefreshToken(userId).Returns(novoRefreshToken);
        _tokenService.GenerateAccessToken(user).Returns("novo-access-token");

        // Act
        var result = await _sut.RefreshAsync("token-valido");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("novo-access-token", result.Value!.AccessToken);
        Assert.Equal("novo-refresh-token", result.Value.RefreshToken);

        // Verifica rotação: token antigo revogado, novo adicionado
        await _repository.Received(1).RevokeRefreshTokenAsync(storedToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).AddRefreshTokenAsync(novoRefreshToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // LogoutAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_DeveRetornarUnauthorized_QuandoTokenNaoEncontrado()
    {
        // Arrange
        _repository.GetRefreshTokenAsync("token-invalido", Arg.Any<CancellationToken>())
                   .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.LogoutAsync("token-invalido");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task LogoutAsync_DeveRetornarUnauthorized_QuandoTokenJaRevogado()
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
    public async Task LogoutAsync_DeveRevogarToken_QuandoValido()
    {
        // Arrange
        var storedToken = new RefreshToken { Token = "token-ativo", IsRevoked = false };
        _repository.GetRefreshTokenAsync("token-ativo", Arg.Any<CancellationToken>())
                   .Returns(storedToken);

        // Act
        var result = await _sut.LogoutAsync("token-ativo");

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).RevokeRefreshTokenAsync(storedToken, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // UpdateUserRolesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserRolesAsync_DeveRetornarNotFound_QuandoUsuarioNaoExiste()
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
    public async Task UpdateUserRolesAsync_DeveRetornarValidation_QuandoListaDeRolesVazia()
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
    public async Task UpdateUserRolesAsync_DeveRetornarValidation_QuandoRoleNaoExisteNoBanco()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var request = new UpdateUserRolesRequest([RoleType.Admin]);

        _repository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
                   .Returns(user);
        // Retorna lista vazia — role não encontrada
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([]);

        // Act
        var result = await _sut.UpdateUserRolesAsync(userId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_DeveAtualizarRoles_QuandoDadosValidos()
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
