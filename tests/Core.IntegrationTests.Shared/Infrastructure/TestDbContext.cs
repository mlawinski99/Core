using Core.DataAccessTypes;
using Core.Identity.Context;
using Core.Identity.Domain;
using Core.Infrastructure.Json;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.KeycloakSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestDbContext : BaseDbContext, IUserContext, IKeycloakEventsContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options, IJsonSerializer jsonSerializer, IEnumerable<IInterceptor> interceptors)
        : base(options, jsonSerializer, interceptors)
    {
    }

    public DbSet<KeycloakAdminEvent> KeycloakAdminEvents { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuditableEntity> AuditableEntities { get; set; }
    public DbSet<AuditableWithUserEntity> AuditableWithUserEntities { get; set; }
    public DbSet<SoftDeletableEntity> SoftDeletableEntities { get; set; }
    public DbSet<VersionableEntity> VersionableEntities { get; set; }
    public DbSet<EncryptableEntity> EncryptableEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new KeycloakAdminEventConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());

        modelBuilder.Entity<AuditableEntity>(entity =>
        {
            entity.ToTable("AuditableEntities");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AuditableWithUserEntity>(entity =>
        {
            entity.ToTable("AuditableWithUserEntities");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SoftDeletableEntity>(entity =>
        {
            entity.ToTable("SoftDeletableEntities");
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<VersionableEntity>(entity =>
        {
            entity.ToTable("VersionableEntities");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<EncryptableEntity>(entity =>
        {
            entity.ToTable("EncryptableEntities");
            entity.HasKey(e => e.Id);
        });
    }
}
