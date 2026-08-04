using Construcheck.Auth.Domain;
using Construcheck.Auth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Construcheck.Auth.Infrastructure.Persistence;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var emailConverter = new ValueConverter<Email, string>(
            email => email.Value,
            value => Email.FromExistingValue(value));

        var hashedPasswordConverter = new ValueConverter<HashedPassword, string>(
            password => password.Value,
            value => HashedPassword.FromExistingHash(value));

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                  .HasConversion(emailConverter)
                  .IsRequired()
                  .HasMaxLength(256)
                  .HasColumnName("Email");

            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.Password)
                  .HasConversion(hashedPasswordConverter)
                  .IsRequired()
                  .HasColumnName("PasswordHash");

            // User é Aggregate Root — RefreshTokens e UserRoles são acessados só através dele,
            // mas fisicamente continuam em tabelas próprias (owned/related, não embutidos)
            entity.HasMany(u => u.RefreshTokens)
                  .WithOne()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Metadata.FindNavigation(nameof(User.RefreshTokens))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);

            entity.Metadata.FindNavigation(nameof(User.UserRoles))!
                  .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.Property(r => r.Description).HasMaxLength(256);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.Property(rt => rt.Token).IsRequired().HasMaxLength(450);
        });

        // Seed das roles — mesmos IDs fixos do sistema original, para não quebrar dados existentes
        var adminId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var viewerId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901");

        modelBuilder.Entity<Role>().HasData(
            Role.Create(adminId, "Admin", "Acesso total — gerencia obras, orçamentos, custos e usuários."),
            Role.Create(viewerId, "Viewer", "Acesso somente leitura — visualiza obras e informações associadas.")
        );

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Antes de persistir, resolve o EntityState correto de todo RefreshToken rastreado.
    ///
    /// O EF Core, ao descobrir uma entidade através de navegação (não via DbSet.Add
    /// direto), decide entre Added/Modified usando uma convenção baseada em: a chave
    /// primária já tem valor não-padrão? Como RefreshToken.Id é um Guid gerado em código
    /// (RefreshToken.Create), essa convenção sempre marca Modified, mesmo para tokens
    /// genuinamente novos — sem correção, isso causa DbUpdateConcurrencyException
    /// ("entity does not exist in the store") ao tentar UPDATE de algo que nunca existiu.
    ///
    /// A correção não pode se basear só em EntityState == Modified, porque um token
    /// genuinamente já existente (revogado via Revoke() em RefreshAsync/LogoutAsync)
    /// também aparece como Modified — forçá-lo para Added causaria erro de chave
    /// duplicada ao tentar inserir de novo algo que já está no banco.
    ///
    /// A única forma inequívoca de diferenciar os dois casos é perguntar ao banco
    /// diretamente (AsNoTracking, ignorando a Identity Map local) se aquele Id já
    /// está persistido.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ResolveRefreshTokenStatesAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ResolveRefreshTokenStatesAsync(CancellationToken ct)
    {
        foreach (var tokenEntry in ChangeTracker.Entries<RefreshToken>().ToList())
        {
            if (tokenEntry.State != EntityState.Modified && tokenEntry.State != EntityState.Detached)
                continue;

            if (tokenEntry.State == EntityState.Detached)
            {
                tokenEntry.State = EntityState.Added;
                continue;
            }

            var tokenId = tokenEntry.Entity.Id;
            var alreadyPersisted = await RefreshTokens
                .AsNoTracking()
                .AnyAsync(t => t.Id == tokenId, ct);

            if (!alreadyPersisted)
                tokenEntry.State = EntityState.Added;
        }
    }
}
