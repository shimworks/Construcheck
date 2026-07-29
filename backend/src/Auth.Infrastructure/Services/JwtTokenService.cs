using Construcheck.Auth.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Construcheck.Auth.Infrastructure.Services;

public class JwtTokenService(IConfiguration configuration) : ITokenService
{
    public string GenerateAccessToken(User user)
    {
        var secret = configuration["JWT_SECRET"]!;
        var issuer = configuration["JWT_ISSUER"]!;
        var audience = configuration["JWT_AUDIENCE"]!;
        var expirationMinutes = int.Parse(configuration["JWT_EXPIRATION_MINUTES"]!);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var userRole in user.UserRoles)
            claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId)
    {
        var expirationDays = int.Parse(configuration["REFRESH_TOKEN_EXPIRATION_DAYS"]!);
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);

        return RefreshToken.Create(userId, tokenValue, expiresAt);
    }
}
