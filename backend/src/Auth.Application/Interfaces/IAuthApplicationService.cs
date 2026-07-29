using Construcheck.Auth.Application.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Auth.Application.Interfaces;

public interface IAuthApplicationService
{
    Task<Result<bool>> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginUserRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<bool>> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken ct = default);
}
