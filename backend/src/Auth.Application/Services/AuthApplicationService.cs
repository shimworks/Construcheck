using Construcheck.Auth.Application.DTOs;
using Construcheck.Auth.Application.Interfaces;
using Construcheck.Auth.Domain;
using Construcheck.SharedKernel;

namespace Construcheck.Auth.Application.Services;

public class AuthApplicationService(IUserRepository repository, ITokenService tokenService) : IAuthApplicationService
{
    public async Task<Result<bool>> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        var existing = await repository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Result<bool>.Conflict("E-mail já cadastrado.");

        var viewerRole = await repository.GetRoleByNameAsync("Viewer", ct);
        if (viewerRole is null)
            return Result<bool>.Failure("Role padrão não encontrada. Verifique o seed do banco.");

        var userResult = User.Create(request.Email, request.Password);
        if (userResult.IsFailure)
            return Result<bool>.Validation(userResult.Error);

        var user = userResult.Value!;
        user.AssignRole(viewerRole);

        await repository.AddAsync(user, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginUserRequest request, CancellationToken ct = default)
    {
        var user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null || !user.VerifyPassword(request.Password))
            return Result<AuthResponse>.Unauthorized("E-mail ou senha inválidos.");

        var refreshToken = tokenService.GenerateRefreshToken(user.Id);
        user.AddRefreshToken(refreshToken);

        var accessToken = tokenService.GenerateAccessToken(user);

        await repository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshToken.Token));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await repository.GetRefreshTokenAsync(refreshToken, ct);

        if (stored is null || !stored.IsValid())
            return Result<AuthResponse>.Unauthorized("Refresh token inválido ou expirado.");

        var user = await repository.GetByIdAsync(stored.UserId, ct);
        if (user is null)
            return Result<AuthResponse>.Unauthorized("Refresh token inválido ou expirado.");

        // Rotação: revoga o token atual e emite um novo
        stored.Revoke();

        var newRefreshToken = tokenService.GenerateRefreshToken(user.Id);
        user.AddRefreshToken(newRefreshToken);

        var accessToken = tokenService.GenerateAccessToken(user);

        await repository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, newRefreshToken.Token));
    }

    public async Task<Result<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await repository.GetRefreshTokenAsync(refreshToken, ct);

        if (stored is null || stored.IsRevoked)
            return Result<bool>.Unauthorized("Refresh token inválido.");

        stored.Revoke();
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken ct = default)
    {
        var user = await repository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        var roleNames = request.Roles.Select(r => r.ToString()).ToList();
        var roles = await repository.GetRolesByNamesAsync(roleNames, ct);
        if (roles.Count != request.Roles.Count)
            return Result<bool>.Validation("Uma ou mais roles informadas não existem.");

        var replaceResult = user.ReplaceRoles(roles);
        if (replaceResult.IsFailure)
            return replaceResult;

        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
