using Microsoft.EntityFrameworkCore;
using Construcheck.Core.Projects.Entities;
using Construcheck.Core.Teams.Entities;
using Construcheck.Core.Contracts.Entities;
using Construcheck.Core.Budget.Entities;
using Construcheck.Core.Schedule.Entities;

namespace Construcheck.Core.Data;

public interface ICoreDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<Team> Teams { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<BudgetItem> BudgetItems { get; }
    DbSet<SchedulePhase> SchedulePhases { get; }
    DbSet<Activity> Activities { get; }
    DbSet<Dependency> Dependencies { get; }
    DbSet<Milestone> Milestones { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}