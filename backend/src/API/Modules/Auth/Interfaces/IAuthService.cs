using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.SharedKernel;
using Microsoft.AspNetCore.Identity.Data;

namespace Construcheck.API.Modules.Auth.Interfaces;

public interface IAuthService
{
    Task<Result<bool>> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginUserRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<bool>> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken ct = default);
}