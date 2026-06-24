using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Interfaces;
using Construcheck.SharedKernel;
using Microsoft.AspNetCore.Identity.Data;

namespace Construcheck.API.Modules.Auth.Services;

public class AuthService(IAuthRepository repository, TokenService tokenService) : IAuthService
{
    public async Task<Result<bool>> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        var existing = await repository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Result<bool>.Conflict("E-mail já cadastrado.");

        var viewerRole = await repository.GetRoleByNameAsync("Viewer", ct);
        if (viewerRole is null)
            return Result<bool>.Failure("Role padrão não encontrada. Verifique o seed do banco.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = viewerRole.Id, Role = viewerRole });

        await repository.AddUserAsync(user, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized("E-mail ou senha inválidos.");

        var refreshToken = tokenService.GenerateRefreshToken(user.Id);
        await repository.AddRefreshTokenAsync(refreshToken, ct);
        await repository.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshToken.Token));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await repository.GetRefreshTokenAsync(refreshToken, ct);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            return Result<AuthResponse>.Unauthorized("Refresh token inválido ou expirado.");

        // Rotação: revoga o token atual e emite um novo
        await repository.RevokeRefreshTokenAsync(stored, ct);

        var newRefreshToken = tokenService.GenerateRefreshToken(stored.UserId);
        await repository.AddRefreshTokenAsync(newRefreshToken, ct);
        await repository.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(stored.User);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, newRefreshToken.Token));
    }

    public async Task<Result<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await repository.GetRefreshTokenAsync(refreshToken, ct);

        if (stored is null || stored.IsRevoked)
            return Result<bool>.Unauthorized("Refresh token inválido.");

        await repository.RevokeRefreshTokenAsync(stored, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken ct = default)
    {
        var user = await repository.GetByIdWithRolesAsync(userId, ct);
        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (request.Roles.Count == 0)
            return Result<bool>.Validation("Informe ao menos uma role.");

        var roleNames = request.Roles.Select(r => r.ToString()).ToList();
        var roles = await repository.GetRolesByNamesAsync(roleNames, ct);
        if (roles.Count != request.Roles.Count)
            return Result<bool>.Validation("Uma ou mais roles informadas não existem.");

        await repository.UpdateUserRolesAsync(user, roles, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}