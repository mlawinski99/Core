using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>();
        builder.HasIndex(x => new { x.ProcessedOn, x.OccurredOnUtc });
        builder.HasIndex(x => new { x.AggregateId, x.ProcessedOn, x.OccurredOnUtc });
    }
}
