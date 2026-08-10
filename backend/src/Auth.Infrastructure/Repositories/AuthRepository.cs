using Construcheck.Auth.Domain;
using Construcheck.Auth.Domain.ValueObjects;
using Construcheck.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Auth.Infrastructure.Repositories;

public class AuthRepository(AuthDbContext db) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {

        var emailResult = Email.Create(email);
        if (emailResult.IsFailure)
            return null; // e-mail malformado nunca vai bater com nada no banco

        return await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Role?> GetRoleByNameAsync(string name, CancellationToken ct = default) =>
        db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);

    public Task<List<Role>> GetRolesByNamesAsync(List<string> names, CancellationToken ct = default) =>
        db.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
