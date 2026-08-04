using Construcheck.Auth.Domain;
using Construcheck.Auth.Infrastructure.Persistence;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Construcheck.Integration.Tests.Infrastructure;

/// <summary>
/// Factory que substitui os DbContext de SQL Server por EF Core InMemory.
/// Cada instância recebe bancos isolados via nome único — sem interferência entre testes.
/// Registra os DOIS Bounded Contexts (Auth e Construction), já que a API real depende
/// de ambos para subir corretamente.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _authDbName = $"construcheck-auth-test-{Guid.NewGuid()}";
    private readonly string _constructionDbName = $"construcheck-construction-test-{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((context, services, configuration) =>
            configuration
                .MinimumLevel.Warning()
                .WriteTo.Console());

        var host = base.CreateHost(builder);

        // InMemory não processa HasData/migrations — o seed de Roles precisa ser
        // aplicado manualmente aqui, mesmo já estando configurado via HasData
        // no AuthDbContext (aquele HasData só é aplicado por migration real).
        using var scope = host.Services.CreateScope();
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        SeedRoles(authDb);

        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs já não registra SQL Server quando o ambiente é "Testing"
            // (ver bloco condicional lá), então aqui só é necessário ADICIONAR o
            // provider InMemory — não existe registro de SQL Server para remover
            // ou entrar em conflito.
            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase(_authDbName));

            services.AddDbContext<ConstructionDbContext>(options =>
                options.UseInMemoryDatabase(_constructionDbName));
        });

        builder.UseSetting("JWT_SECRET", "construcheck-super-secret-key-for-tests-with-256-bits!!");
        builder.UseSetting("JWT_ISSUER", "construcheck-test");
        builder.UseSetting("JWT_AUDIENCE", "construcheck-test");
        builder.UseSetting("JWT_EXPIRATION_MINUTES", "15");
        builder.UseSetting("REFRESH_TOKEN_EXPIRATION_DAYS", "7");
    }

    private static void SeedRoles(AuthDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            Role.Create(
                Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                "Admin",
                "Acesso total."),
            Role.Create(
                Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                "Viewer",
                "Acesso somente leitura.")
        );
        db.SaveChanges();
    }

    /// <summary>
    /// Cria um escopo para manipular o AuthDbContext diretamente nos testes.
    /// Útil para preparar dados (Arrange) e verificar estado após chamadas (Assert).
    /// </summary>
    public AuthDbContext CreateAuthDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    }

    /// <summary>
    /// Equivalente ao CreateAuthDbContext, mas para o Bounded Context de Construction.
    /// Ainda não usado pelos testes de integração existentes (só há testes de Auth
    /// até o momento), mas exposto para quando testes de Construction forem escritos.
    /// </summary>
    public ConstructionDbContext CreateConstructionDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ConstructionDbContext>();
    }
}
