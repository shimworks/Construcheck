using Construcheck.API.Data;
using Construcheck.API.Modules.Auth.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Construcheck.Integration.Tests.Infrastructure;

/// <summary>
/// Factory que substitui o banco SQL Server por EF Core InMemory.
/// Cada instância recebe um banco isolado via nome único — sem interferência entre testes.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"construcheck-test-{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((context, services, configuration) =>
            configuration
                .MinimumLevel.Warning()
                .WriteTo.Console());

        var host = base.CreateHost(builder);

        // InMemory não precisa de EnsureCreated — o banco é criado automaticamente no primeiro uso
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SeedRoles(db);

        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove tudo relacionado ao EF Core
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType.Namespace != null &&
                    d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore"))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // Registra com InMemory
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });

        builder.UseSetting("JWT_SECRET", "construcheck-super-secret-key-for-tests-with-256-bits!!");
        builder.UseSetting("JWT_ISSUER", "construcheck-test");
        builder.UseSetting("JWT_AUDIENCE", "construcheck-test");
        builder.UseSetting("JWT_EXPIRATION_MINUTES", "15");
        builder.UseSetting("REFRESH_TOKEN_EXPIRATION_DAYS", "7");
    }

    private static void SeedRoles(AppDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            new Role
            {
                Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                Name = "Admin",
                Description = "Acesso total."
            },
            new Role
            {
                Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                Name = "Viewer",
                Description = "Acesso somente leitura."
            }
        );
        db.SaveChanges();
    }

    /// <summary>
    /// Cria um escopo para manipular o banco de dados diretamente nos testes.
    /// Útil para preparar dados (Arrange) e verificar estado após chamadas (Assert).
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
