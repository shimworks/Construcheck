using Construcheck.API.Modules.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        base.OnModelCreating(modelBuilder);
    }
}