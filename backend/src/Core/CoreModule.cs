using Construcheck.Core.Contracts.Interfaces;
using Construcheck.Core.Contracts.Repositories;
using Construcheck.Core.Contracts.Services;
using Construcheck.Core.Schedule.Interfaces;
using Construcheck.Core.Schedule.Repositories;
using Construcheck.Core.Schedule.Services;
using Construcheck.Core.Teams.Interfaces;
using Construcheck.Core.Teams.Repositories;
using Construcheck.Core.Teams.Services;
using Construcheck.Core.Projects.Interfaces;
using Construcheck.Core.Projects.Repositories;
using Construcheck.Core.Projects.Services;
using Construcheck.Core.Budget.Interfaces;
using Construcheck.Core.Budget.Repositories;
using Construcheck.Core.Budget.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Construcheck.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, ProjectService>();

        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamService, TeamService>();

        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IContractService, ContractService>();

        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IBudgetService, BudgetService>();
        //services.AddScoped<ISpreadsheetImportService, SpreadsheetImportService>();

        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<IScheduleService, ScheduleService>();

        return services;
    }
}