using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Construcheck.API.Modules.Auth.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Construcheck.API.Modules.Auth.Services;

public class TokenService(IConfiguration configuration)
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
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Adiciona todas as roles do usuário como claims
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

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
    }
}