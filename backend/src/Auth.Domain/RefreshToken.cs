namespace Construcheck.Auth.Domain;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Token = token,
        ExpiresAt = expiresAt,
        IsRevoked = false,
        CreatedAt = DateTime.UtcNow
    };

    public static RefreshToken Reconstitute(
        Guid id, Guid userId, string token, DateTime expiresAt, bool isRevoked, DateTime createdAt) => new()
    {
        Id = id,
        UserId = userId,
        Token = token,
        ExpiresAt = expiresAt,
        IsRevoked = isRevoked,
        CreatedAt = createdAt
    };

    public bool IsValid() => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    public void Revoke() => IsRevoked = true;
}
