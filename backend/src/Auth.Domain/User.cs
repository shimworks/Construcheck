using Construcheck.Auth.Domain.ValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Auth.Domain;

public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public HashedPassword Password { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles;

    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens;

    private User() { }

    public static Result<User> Create(string rawEmail, string plainTextPassword)
    {
        var emailResult = ValueObjects.Email.Create(rawEmail);
        if (emailResult.IsFailure)
            return Result<User>.Validation(emailResult.Error);

        var passwordResult = HashedPassword.Create(plainTextPassword);
        if (passwordResult.IsFailure)
            return Result<User>.Validation(passwordResult.Error);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailResult.Value!,
            Password = passwordResult.Value!,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Reconstrói o User a partir de dados já existentes (vindo do banco).
    /// Não revalida e-mail nem política de senha — ambos já foram validados na criação original.
    /// </summary>
    public static User Reconstitute(
        Guid id, string email, string hashedPasswordValue,
        DateTime createdAt, DateTime updatedAt,
        List<UserRole> userRoles, List<RefreshToken> refreshTokens)
    {
        var user = new User
        {
            Id = id,
            Email = ValueObjects.Email.FromExistingValue(email),
            Password = HashedPassword.FromExistingHash(hashedPasswordValue),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        user._userRoles.AddRange(userRoles);
        user._refreshTokens.AddRange(refreshTokens);

        return user;
    }

    public bool VerifyPassword(string plainTextPassword) =>
        Password.Verify(plainTextPassword);

    public void AssignRole(Role role)
    {
        if (_userRoles.Any(ur => ur.RoleId == role.Id))
            return; // já tem essa role, operação idempotente

        _userRoles.Add(new UserRole { UserId = Id, RoleId = role.Id, Role = role });
        UpdatedAt = DateTime.UtcNow;
    }

    public Result<bool> ReplaceRoles(List<Role> newRoles)
    {
        if (newRoles.Count == 0)
            return Result<bool>.Validation("Informe ao menos uma role.");

        _userRoles.Clear();
        foreach (var role in newRoles)
            _userRoles.Add(new UserRole { UserId = Id, RoleId = role.Id, Role = role });

        UpdatedAt = DateTime.UtcNow;
        return Result<bool>.Success(true);
    }

    public void AddRefreshToken(RefreshToken token) => _refreshTokens.Add(token);
}
