using Core.DataAccessTypes;
using Core.Identity.Context;
using Core.Identity.Domain;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.KeycloakSync;
using Core.Outbox;
using Core.Permissions;
using Core.Permissions.Configurations;
using Core.Permissions.Entities;
using Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestDbContext(
    DbContextOptions<TestDbContext> options,
    IEnumerable<IInterceptor> interceptors)
    : BaseDbContext(options, interceptors), IUserContext, IKeycloakEventsContext, IOutbox, IFileStore, IPermissionContext
{
    public DbSet<KeycloakAdminEvent> KeycloakAdminEvents { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuditableEntity> AuditableEntities { get; set; }
    public DbSet<AuditableWithUserEntity> AuditableWithUserEntities { get; set; }
    public DbSet<SoftDeletableEntity> SoftDeletableEntities { get; set; }
    public DbSet<VersionableEntity> VersionableEntities { get; set; }
    public DbSet<EncryptableEntity> EncryptableEntities { get; set; }
    public DbSet<TestEntity> TestEntities { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<ProcessedOutboxMessage> ProcessedOutboxMessages { get; set; }
    public DbSet<DeadLetterOutboxMessage> DeadLetterOutboxMessages { get; set; }
    public DbSet<StoredFile> StoredFiles { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

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
            entity.WithId().WithVersionable();
        });

        modelBuilder.Entity<EncryptableEntity>(entity =>
        {
            entity.ToTable("EncryptableEntities");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.ToTable("TestEntities");
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
        });

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedOutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new DeadLetterOutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new StoredFileConfiguration());
        modelBuilder.ApplyConfiguration(new UserPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
    }
}
