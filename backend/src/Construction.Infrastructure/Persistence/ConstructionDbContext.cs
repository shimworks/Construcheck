using Construcheck.Construction.Domain.Budget;
using Construcheck.Construction.Domain.Contracts;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.Construction.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Construcheck.Construction.Infrastructure.Persistence;

public class ConstructionDbContext(DbContextOptions<ConstructionDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<SchedulePhase> SchedulePhases => Set<SchedulePhase>();
    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var moneyConverter = new ValueConverter<Money, decimal>(
            money => money.Amount,
            amount => Money.FromExistingValue(amount));

        ConfigureProject(modelBuilder);
        ConfigureTeam(modelBuilder);
        ConfigureContract(modelBuilder, moneyConverter);
        ConfigureBudgetItem(modelBuilder, moneyConverter);
        ConfigureSchedulePhase(modelBuilder);
        ConfigureActivity(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Address).HasMaxLength(300);
            entity.Property(p => p.TechnicalManager).HasMaxLength(200);

            // DateRange (Value Object com 2 campos) mapeado via ComplexProperty —
            // Schedule.Start e Schedule.End viram colunas ScheduleStart/ScheduleEnd na tabela.
            entity.ComplexProperty(p => p.Schedule, schedule =>
            {
                schedule.Property(s => s.Start).HasColumnName("StartDate");
                schedule.Property(s => s.End).HasColumnName("TargetEndDate");
            });
        });
    }

    private static void ConfigureTeam(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(t => t.ProjectId);
        });
    }

    private static void ConfigureContract(ModelBuilder modelBuilder, ValueConverter<Money, decimal> moneyConverter)
    {
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CounterpartyName).IsRequired().HasMaxLength(200);
            entity.HasIndex(c => c.ProjectId);

            entity.Property(c => c.Value)
                  .HasConversion(moneyConverter)
                  .HasColumnType("decimal(18,2)")
                  .HasColumnName("Value");

            entity.ComplexProperty(c => c.Term, term =>
            {
                term.Property(t => t.Start).HasColumnName("StartDate");
                term.Property(t => t.End).HasColumnName("DueDate");
            });

            // Índice sobre a coluna real (DueDate) — usado pela Fase 7 (Alertas)
            entity.HasIndex("DueDate");
        });
    }

    private static void ConfigureBudgetItem(ModelBuilder modelBuilder, ValueConverter<Money, decimal> moneyConverter)
    {
        modelBuilder.Entity<BudgetItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Description).IsRequired().HasMaxLength(300);
            entity.Property(i => i.Quantity).HasColumnType("decimal(18,4)");
            entity.HasIndex(i => i.ProjectId);

            entity.Property(i => i.UnitPrice)
                  .HasConversion(moneyConverter)
                  .HasColumnType("decimal(18,2)")
                  .HasColumnName("UnitPrice");

            entity.Ignore(i => i.TotalValue); // calculado, não persiste
        });
    }

    private static void ConfigureSchedulePhase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchedulePhase>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
        });
    }

    private static void ConfigureActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(a => a.SchedulePhaseId);

            // Índice composto: a query de recálculo em cascata (GetByPredecessorIdAsync)
            // sempre filtra por ProjectId + DeletionStatus juntos.
            entity.HasIndex(a => new { a.ProjectId, a.DeletionStatus });

            entity.ComplexProperty(a => a.PlannedPeriod, period =>
            {
                period.Property(p => p.Start).HasColumnName("PlannedStartDate");
                period.Property(p => p.End).HasColumnName("PlannedEndDate");
            });

            // PredecessorIds é lista de Guid simples — EF Core 8+ mapeia List<Guid> como
            // coluna JSON automaticamente quando não há configuração explícita de tabela própria.
            entity.PrimitiveCollection(a => a.PredecessorIds).HasColumnName("PredecessorIds");
        });
    }
}
