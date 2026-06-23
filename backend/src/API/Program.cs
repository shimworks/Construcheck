using Construcheck.API.Data;
using Construcheck.API.Modules.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

try
{
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

    // Connection string — em produção vem de variável de ambiente injetada pelo pipeline
    // Em desenvolvimento vem do .env
    string connectionString = string.Empty;
    var server = builder.Configuration["DB_SERVER"];
    var port = builder.Configuration["DB_PORT"];
    var database = builder.Configuration["DB_NAME"];
    var user = builder.Configuration["DB_USER"];
    var password = builder.Configuration["DB_PASSWORD"];
    if (string.IsNullOrEmpty(server)) Log.Error("***************************************** SERVER IS EMPTY ***********************************************************");

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


    // Banco de dados com retry automático para falhas transientes em runtime
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

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
    builder.Services.AddAuthModule();

    // Exception Handler centralizado
    builder.Services.AddExceptionHandler<Construcheck.API.Middleware.GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS para o Angular
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Angular", policy =>
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000}ms";
    });

    // Aplica migrations com retry — tolera banco momentaneamente indisponível no startup
    using (var scope = app.Services.CreateScope())
    {
        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(5);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
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

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseExceptionHandler();
    app.UseCors("Angular");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação encerrou inesperadamente durante o boot");
}
finally
{
    await Log.CloseAndFlushAsync();
}