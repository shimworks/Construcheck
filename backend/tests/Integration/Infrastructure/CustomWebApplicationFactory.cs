using Construcheck.Auth.Domain;
using Construcheck.Auth.Infrastructure.Persistence;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net.Http.Json;
using System.Text.Json;

namespace Construcheck.Integration.Tests.Infrastructure;

/// <summary>
/// Factory que substitui os DbContext de SQL Server pelos providers de teste.
/// Cada instância recebe bancos isolados — sem interferência entre testes.
/// Registra os DOIS Bounded Contexts (Auth e Construction), já que a API real depende
/// de ambos para subir corretamente.
///
/// AuthDbContext: EF Core InMemory. Suficiente porque AuthDbContext só usa HasConversion
/// (Email, HashedPassword) — nenhum ComplexProperty, então não bate na limitação abaixo.
///
/// ConstructionDbContext: SQLite in-memory (não o provider InMemory do EF Core). Necessário
/// porque Project.Schedule, Contract.Term e Activity.PlannedPeriod usam ComplexProperty
/// sobre DateRange, e o provider InMemory do EF Core falha ao montar (shape) o resultado
/// dessas entidades de volta a partir de uma query — lança KeyNotFoundException em
/// qualquer leitura (GetById, GetAll, etc). SQLite é um motor relacional real (só que
/// em memória), então processa ComplexProperty do mesmo jeito que o SQL Server de produção.
///
/// CUIDADO COM O CICLO DE VIDA DA CONEXÃO SQLite: "DataSource=:memory:" cria um banco
/// NOVO E ISOLADO a cada conexão física aberta. Como CreateConstructionDbContext() e o
/// próprio DI resolvem um DbContext novo a cada scope, usar uma connection string direta
/// faria cada resolução apontar para um banco vazio diferente — quebrando silenciosamente
/// qualquer Assert que tenta ler o que uma chamada HTTP anterior escreveu. A correção é
/// manter UMA ÚNICA SqliteConnection aberta durante toda a vida da factory e configurar
/// UseSqlite(connection) (a instância, não a string) — assim todo DbContext resolvido
/// compartilha exatamente o mesmo banco em memória enquanto a conexão permanecer aberta.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _authDbName = $"construcheck-auth-test-{Guid.NewGuid()}";
    private readonly SqliteConnection _constructionConnection;

    public CustomWebApplicationFactory()
    {
        // Aberta uma única vez aqui, no construtor da factory, e mantida viva até
        // Dispose(). Nunca fechada entre chamadas — é essa conexão persistente que
        // faz o banco em memória "existir" de forma compartilhada entre todos os
        // DbContext resolvidos pelo DI durante a vida desta instância de factory.
        _constructionConnection = new SqliteConnection("DataSource=:memory:");
        _constructionConnection.Open();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((context, services, configuration) =>
            configuration
                .MinimumLevel.Warning()
                .WriteTo.Console());

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();

        // InMemory não processa HasData/migrations — o seed de Roles precisa ser
        // aplicado manualmente aqui, mesmo já estando configurado via HasData
        // no AuthDbContext (aquele HasData só é aplicado por migration real).
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        SeedRoles(authDb);

        // SQLite in-memory: schema não existe até ser criado explicitamente.
        // EnsureCreated() gera o schema a partir do MODELO do EF Core (não das
        // migrations reais, que têm sintaxe específica de SQL Server e falhariam
        // aqui). ConstructionDbContext não usa HasData em lugar nenhum, então
        // EnsureCreated() é suficiente — não há seed de dados para replicar.
        var constructionDb = scope.ServiceProvider.GetRequiredService<ConstructionDbContext>();
        constructionDb.Database.EnsureCreated();

        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs já não registra SQL Server quando o ambiente é "Testing"
            // (ver bloco condicional lá), então aqui só é necessário ADICIONAR os
            // providers de teste — não existe registro de SQL Server para remover
            // ou entrar em conflito.
            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase(_authDbName));

            services.AddDbContext<ConstructionDbContext>(options =>
                options.UseSqlite(_constructionConnection));
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
    /// Usado pelas suites de integração de Construction para preparar dados (Arrange)
    /// e verificar estado persistido após chamadas HTTP (Assert) — por exemplo, confirmar
    /// que um recálculo em cascata realmente moveu as datas planejadas de uma Activity
    /// dependente no banco, não apenas que a resposta HTTP teve status 200.
    ///
    /// Cada chamada resolve um DbContext NOVO (via scope novo), mas todos compartilham
    /// o mesmo banco SQLite em memória através de _constructionConnection — por isso
    /// dados escritos por uma chamada HTTP anterior (via outro DbContext, resolvido
    /// pelo DI dentro do pipeline da requisição) continuam visíveis aqui.
    /// </summary>
    public ConstructionDbContext CreateConstructionDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ConstructionDbContext>();
    }

    // -------------------------------------------------------------------------
    // Auth helpers compartilhados — toda rota de Construction exige Bearer token,
    // e a maioria exige role Admin. Extraídos aqui (em vez de duplicados em cada
    // classe de teste de Construction) porque a Factory já é o ponto compartilhado
    // via IClassFixture em todas as suites de integração, e o padrão de
    // "registrar usuário e promover a Admin direto no banco" já existia como
    // método PRIVADO em AuthEndpointsTests.CreateAdminAndGetToken — este helper
    // faz o mesmo, mas de forma reutilizável entre suites diferentes.
    //
    // Usam HTTP real (via CreateClient() interno), não chamada direta a serviços,
    // para que o token gerado percorra o mesmo caminho de autenticação real que
    // qualquer requisição de teste vai usar depois.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registra um novo usuário (role Viewer, padrão de AuthApplicationService.RegisterAsync)
    /// e retorna o access token JWT já autenticado. Usa um e-mail único por chamada
    /// para nunca colidir com outro teste rodando na mesma instância de factory.
    /// </summary>
    public async Task<string> RegisterUserAndGetTokenAsync(string? emailPrefix = null)
    {
        using var client = CreateClient();
        var email = $"{emailPrefix ?? "user"}-{Guid.NewGuid()}@test.com";
        const string password = "Password123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        if (!registerResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Falha ao registrar usuário de teste '{email}': {registerResponse.StatusCode}");

        return await GetAccessTokenAsync(client, email, password);
    }

    /// <summary>
    /// Registra um novo usuário, promove para Admin diretamente no banco (contornando
    /// o endpoint de roles, que por sua vez exige um Admin pré-existente — ovo e galinha
    /// resolvido do mesmo jeito que AuthEndpointsTests.CreateAdminAndGetToken já resolve),
    /// e retorna o access token JWT com a claim de role Admin.
    /// </summary>
    public async Task<string> CreateAdminAndGetTokenAsync(string? emailPrefix = null)
    {
        using var client = CreateClient();
        var email = $"{emailPrefix ?? "admin"}-{Guid.NewGuid()}@test.com";
        const string password = "Admin123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        if (!registerResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Falha ao registrar usuário admin de teste '{email}': {registerResponse.StatusCode}");

        using var db = CreateAuthDbContext();
        var user = db.Users
            .Include(u => u.UserRoles)
            .AsEnumerable()
            .First(u => u.Email.Value == email);
        var adminRole = db.Roles.First(r => r.Name == "Admin");

        var replaceResult = user.ReplaceRoles([adminRole]);
        if (replaceResult.IsFailure)
            throw new InvalidOperationException(
                $"Falha ao promover usuário de teste '{email}' a Admin: {replaceResult.Error}");

        db.SaveChanges();

        return await GetAccessTokenAsync(client, email, password);
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Falha ao autenticar usuário de teste '{email}': {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("accessToken").GetString()!;
    }

    /// <summary>
    /// Fecha e libera a conexão SQLite em memória junto com a factory. Sem isso, a
    /// conexão ficaria aberta indefinidamente após os testes terminarem — vazamento
    /// de recurso, não corrupção de dados (SQLite in-memory não persiste em disco de
    /// qualquer forma), mas ainda assim incorreto não liberar explicitamente.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _constructionConnection.Dispose();

        base.Dispose(disposing);
    }
}
