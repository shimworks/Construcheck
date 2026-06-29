using Construcheck.API.Modules.Auth.Entities;

namespace Construcheck.API.Modules.Auth.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(Guid userId);
}