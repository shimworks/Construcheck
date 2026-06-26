using Construcheck.API.Data;
using Construcheck.API.Modules.Auth.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Construcheck.Integration.Tests.Infrastructure;

/// <summary>
/// Factory que substitui o banco SQL Server por EF Core InMemory.
/// Cada instância recebe um banco isolado via nome único — sem interferência entre testes.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"construcheck-test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o AppDbContext registrado pela aplicação (SQL Server)
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Registra o AppDbContext com InMemory
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Garante que o banco foi criado e faz seed das roles
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            SeedRoles(db);
        });

        // Variáveis de ambiente necessárias para JWT e refresh token
        builder.UseSetting("JWT_SECRET", "construcheck-super-secret-key-para-testes-com-256-bits!!");
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
    public AppDbContext CriarDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
