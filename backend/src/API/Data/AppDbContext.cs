using Construcheck.API.Modules.Auth.Entities;
using Construcheck.Core.Data;
using Construcheck.Core.Projects.Entities;
using Construcheck.Core.Teams.Entities;
using Construcheck.Core.Contracts.Entities;
using Construcheck.Core.Budget.Entities;
using Construcheck.Core.Schedule.Entities;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), ICoreDbContext
{
    // Auth
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Core
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<SchedulePhase> SchedulePhases => Set<SchedulePhase>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<Milestone> Milestones => Set<Milestone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.Property(r => r.Description).HasMaxLength(256);
        });

        // UserRole (chave composta)
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.Property(rt => rt.Token).IsRequired();

            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId);
        });

        // Seed das roles
        var adminId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var viewerId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901");

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = adminId,
                Name = "Admin",
                Description = "Acesso total — gerencia obras, orçamentos, custos e usuários."
            },
            new Role
            {
                Id = viewerId,
                Name = "Viewer",
                Description = "Acesso somente leitura — visualiza obras e informações associadas."
            }
        );

        ConfigureCoreEntities(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureCoreEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Address).HasMaxLength(300);
            entity.Property(p => p.TechnicalManager).HasMaxLength(200);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(t => t.ProjectId);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CounterpartyName).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Value).HasColumnType("decimal(18,2)");
            entity.HasIndex(c => c.ProjectId);
            entity.HasIndex(c => c.DueDate); // usado pela Fase 7 (Alertas)
        });

        modelBuilder.Entity<BudgetItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Description).IsRequired().HasMaxLength(300);
            entity.Property(i => i.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Ignore(i => i.TotalValue); // calculado, não persiste
            entity.HasIndex(i => i.ProjectId);
        });

        modelBuilder.Entity<SchedulePhase>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.HasMany(s => s.Activities)
                  .WithOne(a => a.SchedulePhase)
                  .HasForeignKey(a => a.SchedulePhaseId);
        });

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
        });

        // Dependency tem duas FKs para Activity — Restrict nas duas, senão o SQL Server
        // recusa a migration por causa de múltiplos caminhos de cascade delete na mesma tabela
        modelBuilder.Entity<Dependency>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.HasOne(d => d.Activity)
                  .WithMany(a => a.Dependencies)
                  .HasForeignKey(d => d.ActivityId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PredecessorActivity)
                  .WithMany()
                  .HasForeignKey(d => d.PredecessorActivityId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(150);
        });
    }
}