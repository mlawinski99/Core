using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.KeycloakSync;

public class KeycloakAdminEventConfiguration : IEntityTypeConfiguration<KeycloakAdminEvent>
{
    public void Configure(EntityTypeBuilder<KeycloakAdminEvent> builder)
    {
        builder.ToTable("KeycloakAdminEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityAlwaysColumn();
        builder.Property(e => e.OperationType).HasMaxLength(20);
        builder.Property(e => e.ResourceType).HasMaxLength(50);
        builder.Property(e => e.ResourcePath).HasMaxLength(255);
    }
}