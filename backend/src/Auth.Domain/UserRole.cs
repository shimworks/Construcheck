namespace Construcheck.Auth.Domain;

public class UserRole
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
    public Role Role { get; init; } = null!;
}
