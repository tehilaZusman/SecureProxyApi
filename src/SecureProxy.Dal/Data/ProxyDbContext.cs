using Microsoft.EntityFrameworkCore;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Data;

public class ProxyDbContext : DbContext
{
    public ProxyDbContext(DbContextOptions<ProxyDbContext> options) : base(options)
    {
    }

    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<ParameterRule> ParameterRules => Set<ParameterRule>();
    public DbSet<HeaderRule> HeaderRules => Set<HeaderRule>();
    public DbSet<EncryptionProfile> EncryptionProfiles => Set<EncryptionProfile>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<ConfigurationChangeLog> ConfigurationChangeLogs => Set<ConfigurationChangeLog>();

    private bool IsSqlServer => Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Destination>(e =>
        {
            e.ToTable("Destinations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.BaseUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.AllowedPorts).HasMaxLength(200);
            e.Property(x => x.AuthProfileRef).HasMaxLength(200);
            ConfigureRowVersion(e.Property(x => x.RowVersion));
        });

        modelBuilder.Entity<Operation>(e =>
        {
            e.ToTable("Operations");
            e.HasKey(x => x.Id);
            e.Property(x => x.OperationKey).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.OperationKey).IsUnique();
            e.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            e.Property(x => x.RelativePathTemplate).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Destination)
                .WithMany(d => d.Operations)
                .HasForeignKey(x => x.DestinationId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EncryptionProfile)
                .WithMany()
                .HasForeignKey(x => x.EncryptionProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            ConfigureRowVersion(e.Property(x => x.RowVersion));
        });

        modelBuilder.Entity<ParameterRule>(e =>
        {
            e.ToTable("ParameterRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Pattern).HasMaxLength(500);
            e.HasOne(x => x.Operation)
                .WithMany(o => o.ParameterRules)
                .HasForeignKey(x => x.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OperationId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<HeaderRule>(e =>
        {
            e.ToTable("HeaderRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.HeaderName).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Operation)
                .WithMany(o => o.HeaderRules)
                .HasForeignKey(x => x.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OperationId, x.HeaderName }).IsUnique();
        });

        modelBuilder.Entity<EncryptionProfile>(e =>
        {
            e.ToTable("EncryptionProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Algorithm).HasMaxLength(50).IsRequired();
            e.Property(x => x.KeyRef).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<RequestLog>(e =>
        {
            e.ToTable("RequestLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            e.Property(x => x.OperationKey).HasMaxLength(200);
            e.Property(x => x.ReasonCode).HasMaxLength(100);
            e.HasIndex(x => x.CorrelationId);
            e.HasIndex(x => x.StartedAtUtc);
        });

        modelBuilder.Entity<ConfigurationChangeLog>(e =>
        {
            e.ToTable("ConfigurationChangeLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.Property(x => x.ChangedBy).HasMaxLength(200);
        });
    }

    private void ConfigureRowVersion(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<byte[]?> builder)
    {
        if (IsSqlServer)
        {
            // Native SQL Server rowversion: auto-maintained 8-byte token.
            builder.IsRowVersion();
        }
        else
        {
            // SQLite/InMemory have no rowversion type. Emulate an app-maintained concurrency
            // token so the same model and concurrency semantics work in tests.
            builder.IsConcurrencyToken();
        }
    }
}
