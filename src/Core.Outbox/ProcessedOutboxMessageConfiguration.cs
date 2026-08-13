using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Outbox;

public class ProcessedOutboxMessageConfiguration : IEntityTypeConfiguration<ProcessedOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedOutboxMessage> builder)
    {
        builder.ToTable("ProcessedOutboxMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Content).IsRequired();
    }
}