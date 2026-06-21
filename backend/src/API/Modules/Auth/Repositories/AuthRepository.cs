using Construcheck.API.Data;
using Construcheck.API.Modules.Auth.Entities;
using Construcheck.API.Modules.Auth.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.API.Modules.Auth.Repositories;

public class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default) =>
        db.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Role?> GetRoleByNameAsync(string name, CancellationToken ct = default) =>
        db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);

    public Task<List<Role>> GetRolesByIdsAsync(List<Guid> roleIds, CancellationToken ct = default) =>
        db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default) =>
        db.RefreshTokens
          .Include(rt => rt.User)
          .ThenInclude(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(rt => rt.Token == token, ct);

    public Task RevokeRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        token.IsRevoked = true;
        return Task.CompletedTask;
    }

    public async Task UpdateUserRolesAsync(User user, List<Role> newRoles, CancellationToken ct = default)
    {
        var existing = db.UserRoles.Where(ur => ur.UserId == user.Id);
        db.UserRoles.RemoveRange(existing);

        foreach (var role in newRoles)
            await db.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = role.Id }, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}