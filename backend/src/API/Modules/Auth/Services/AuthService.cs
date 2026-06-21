using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Interfaces;
using Construcheck.SharedKernel;
using Microsoft.AspNetCore.Identity.Data;

namespace Construcheck.API.Modules.Auth.Services;

public class AuthService(IAuthRepository repository, TokenService tokenService) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await repository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Result<AuthResponse>.Conflict("E-mail já cadastrado.");

        var viewerRole = await repository.GetRoleByNameAsync("Viewer", ct);
        if (viewerRole is null)
            return Result<AuthResponse>.Failure("Role padrão não encontrada. Verifique o seed do banco.");

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

        var refreshToken = tokenService.GenerateRefreshToken(user.Id);
        await repository.AddRefreshTokenAsync(refreshToken, ct);
        await repository.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshToken.Token));
    }

    // Login, Refresh, Logout e UpdateUserRoles — implementados nos tópicos seguintes
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

    public Task<Result<bool>> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();
}