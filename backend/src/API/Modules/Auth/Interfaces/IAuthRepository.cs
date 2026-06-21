using Construcheck.API.Modules.Auth.Entities;

namespace Construcheck.API.Modules.Auth.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetRoleByNameAsync(string name, CancellationToken ct = default);
    Task<List<Role>> GetRolesByIdsAsync(List<Guid> roleIds, CancellationToken ct = default);
    Task AddUserAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task UpdateUserRolesAsync(User user, List<Role> newRoles, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}