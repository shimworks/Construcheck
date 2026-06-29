using Construcheck.API.Modules.Auth.Interfaces;
using Construcheck.API.Modules.Auth.Repositories;
using Construcheck.API.Modules.Auth.Services;

namespace Construcheck.API.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        return services;
    }
}