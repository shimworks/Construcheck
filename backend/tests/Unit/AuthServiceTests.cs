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
    public async Task RegisterAsync_ShouldSucceed_WhenPasswordIsExactlyMinimumLength()
    {
        // Arrange — boundary: HashedPassword.ValidatePolicy usa "Length < MinLength" (MinLength = 8).
        // "Passwo1!" tem exatamente 8 caracteres, maiúscula, número e símbolo — deve passar,
        // já que 8 < 8 é falso. Nenhum teste existente cobria esse limite exato; só havia
        // "weak" (claramente inválida) e senhas óbvias mais longas.
        const string exactlyEightChars = "Passwo1!";
        Assert.Equal(8, exactlyEightChars.Length); // guarda contra o teste ficar obsoleto se a string mudar

        var request = new RegisterUserRequest("boundary@test.com", exactlyEightChars);
        var viewerRole = Role.Create(Guid.NewGuid(), "Viewer", "Acesso somente leitura.");

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(viewerRole);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnValidation_WhenPasswordIsOneCharacterBelowMinimumLength()
    {
        // Arrange — o outro lado do boundary: 7 caracteres deve falhar (7 < 8 é verdadeiro).
        // Par com o teste acima; sem este, o boundary de RegisterAsync_ShouldSucceed_WhenPasswordIsExactlyMinimumLength
        // não prova que o limite está no lugar certo, só que 8 funciona.
        const string sevenChars = "Passwo1";
        Assert.Equal(7, sevenChars.Length);

        var request = new RegisterUserRequest("boundary-fail@test.com", sevenChars);
        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
                   .Returns((User?)null);
        _repository.GetRoleByNameAsync("Viewer", Arg.Any<CancellationToken>())
                   .Returns(Role.Create(Guid.NewGuid(), "Viewer", "Acesso somente leitura."));

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Contains("no mínimo 8 caracteres", result.Error);
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
    public async Task UpdateUserRolesAsync_ShouldReturnValidation_WhenRequestedRolesContainDuplicates()
    {
        // Arrange — request pede a mesma role duas vezes: [Admin, Admin].
        // O repositório real busca por NOME (GetRolesByNamesAsync), então nomes duplicados
        // resolvem para uma única Role distinta — o mock replica esse comportamento devolvendo
        // uma lista de 1 mesmo para uma requisição de 2 nomes, como o repositório real faria.
        // Isso dispara a checagem existente "roles.Count != request.Roles.Count" (1 != 2),
        // então o resultado esperado é falha por Validation — não um "sucesso silencioso"
        // colapsando para uma lista de 1, como se poderia supor sem ler o código de perto.
        var user = User.Create("user@test.com", "Password123!").Value!;
        var adminRole = Role.Create(Guid.NewGuid(), "Admin", "Acesso total.");
        var request = new UpdateUserRolesRequest([RoleType.Admin, RoleType.Admin]);

        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
                   .Returns(user);
        _repository.GetRolesByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
                   .Returns([adminRole]); // repositório real dedupe por nome: 2 nomes iguais -> 1 role

        // Act
        var result = await _sut.UpdateUserRolesAsync(user.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Uma ou mais roles informadas não existem.", result.Error);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ReplaceRoles_ShouldAllowDuplicateUserRoleEntries_WhenSameRolePassedTwice()
    {
        // Arrange — este é um teste de DOMÍNIO (User.ReplaceRoles), não do Application Service.
        // Achado relevante: User.ReplaceRoles não filtra duplicata por conta própria — ele confia
        // inteiramente em quem chama (o Application Service, via checagem de contagem acima) para
        // nunca passar uma lista com Role repetida. Se ReplaceRoles for chamado diretamente com
        // a MESMA Role duas vezes (contornando o Application Service), ele aceita e cria duas
        // entradas UserRole com o mesmo RoleId — não há Distinct() nem checagem de RoleId repetido
        // dentro do método. Este teste documenta esse comportamento atual explicitamente, para que
        // uma futura mudança de comportamento (adicionar proteção) seja uma decisão consciente,
        // não uma regressão silenciosa.
        var user = User.Create("user@test.com", "Password123!").Value!;
        var adminRole = Role.Create(Guid.NewGuid(), "Admin", "Acesso total.");

        // Act
        var result = user.ReplaceRoles([adminRole, adminRole]);

        // Assert — comportamento atual documentado: aceita e duplica, não filtra.
        Assert.True(result.IsSuccess);
        Assert.Equal(2, user.UserRoles.Count);
        Assert.All(user.UserRoles, ur => Assert.Equal(adminRole.Id, ur.RoleId));
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
