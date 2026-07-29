namespace Construcheck.Auth.Domain;

public class Role
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles;

    private Role() { }

    public static Role Create(Guid id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description
    };
}
