using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Outbox;

public class DeadLetterOutboxMessageConfiguration : IEntityTypeConfiguration<DeadLetterOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterOutboxMessage> builder)
    {
        builder.ToTable("DeadLetterOutboxMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.HasIndex(x => new { x.AggregateId, x.OccurredOnUtc });
    }
}