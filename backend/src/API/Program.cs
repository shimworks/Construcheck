using Construcheck.Auth.Application.Interfaces;
using Construcheck.Auth.Application.Services;
using Construcheck.Auth.Domain;
using Construcheck.Auth.Infrastructure.Persistence;
using Construcheck.Auth.Infrastructure.Repositories;
using Construcheck.Auth.Infrastructure.Services;
using Construcheck.Construction.Application.Budget.Interfaces;
using Construcheck.Construction.Application.Budget.Services;
using Construcheck.Construction.Application.Contracts.Interfaces;
using Construcheck.Construction.Application.Contracts.Services;
using Construcheck.Construction.Application.Projects.Interfaces;
using Construcheck.Construction.Application.Projects.Services;
using Construcheck.Construction.Application.Schedule.Interfaces;
using Construcheck.Construction.Application.Schedule.Services;
using Construcheck.Construction.Application.Teams.Interfaces;
using Construcheck.Construction.Application.Teams.Services;
using Construcheck.Construction.Domain.Budget;
using Construcheck.Construction.Domain.Contracts;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.DomainServices;
using Construcheck.Construction.Domain.Teams;
using Construcheck.Construction.Infrastructure.Persistence;
using Construcheck.Construction.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Text;

// Bootstrap logger — captura erros durante o boot
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .WriteTo.File(
        new Serilog.Formatting.Compact.CompactJsonFormatter(),
        path: "logs/construcheck-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateBootstrapLogger();

Log.Information("Iniciando Construcheck API");

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
        .WriteTo.File(
            new Serilog.Formatting.Compact.CompactJsonFormatter(),
            path: "logs/construcheck-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7));

// Connection string — mesma lógica original; Cenário A confirmado: um banco físico,
// dois DbContext (AuthDbContext e, futuramente, ConstructionDbContext) apontando pra ele
string connectionString = string.Empty;
var server = builder.Configuration["DB_SERVER"];
var port = builder.Configuration["DB_PORT"];
var database = builder.Configuration["DB_NAME"];
var user = builder.Configuration["DB_USER"];
var password = builder.Configuration["DB_PASSWORD"];

if (builder.Environment.IsProduction())
{
    connectionString = $"Server=tcp:{server},{port};Initial " +
        $"Catalog={database};Persist Security Info=False;" +
        $"User ID={user};Password={password};" +
        $"MultipleActiveResultSets=False;Encrypt=True;" +
        $"TrustServerCertificate=False;Connection Timeout=30;";
}
else
{
    connectionString = $"Server={server},{port};" +
        $"Database={database};" +
        $"User Id={user};" +
        $"Password={password};" +
        "TrustServerCertificate=True;";
}

// ==================== BOUNDED CONTEXT: AUTH ====================

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<IUserRepository, AuthRepository>();
builder.Services.AddScoped<IAuthApplicationService, AuthApplicationService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// ==================== BOUNDED CONTEXT: CONSTRUCTION ====================

builder.Services.AddDbContext<ConstructionDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

// Repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IBudgetItemRepository, BudgetItemRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<ISchedulePhaseRepository, SchedulePhaseRepository>();

// Application Services
builder.Services.AddScoped<IProjectApplicationService, ProjectApplicationService>();
builder.Services.AddScoped<ITeamApplicationService, TeamApplicationService>();
builder.Services.AddScoped<IContractApplicationService, ContractApplicationService>();
builder.Services.AddScoped<IBudgetApplicationService, BudgetApplicationService>();
builder.Services.AddScoped<IScheduleApplicationService, ScheduleApplicationService>();

// Domain Services (Schedule) — stateless, escopo por requisição é suficiente
builder.Services.AddScoped<ActivityStartValidationService>();
builder.Services.AddScoped<ActivityCascadeRescheduleService>();
builder.Services.AddScoped<ActivityReorderService>();
builder.Services.AddScoped<SchedulePhaseDeletionService>();

// JWT
var jwtSecret = builder.Configuration["JWT_SECRET"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JWT_ISSUER"],
            ValidAudience = builder.Configuration["JWT_AUDIENCE"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<Construcheck.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Construcheck API",
        Version = "v1"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT desta forma: Bearer {seu_token}"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000}ms";
});

// Aplica migrations com retry — tolera banco momentaneamente indisponível no startup.
// NOTA: com dois DbContext, cada um migra independentemente. ConstructionDbContext.Migrate()
// será adicionado aqui quando essa camada existir.
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(5);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                authDb.Database.Migrate();

                var constructionDb = scope.ServiceProvider.GetRequiredService<ConstructionDbContext>();
                constructionDb.Database.Migrate();

                break;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw;
                Log.Warning(ex, "Banco indisponível. Tentativa {Attempt} de {Max}. Aguardando...", i + 1, maxRetries);
                await Task.Delay(delay);
            }
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
