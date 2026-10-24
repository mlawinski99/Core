using Microsoft.EntityFrameworkCore;

namespace Core.Outbox;

public interface IOutbox
{
    DbSet<OutboxMessage> OutboxMessages { get; set; }
    DbSet<ProcessedOutboxMessage> ProcessedOutboxMessages { get; set; }
    DbSet<DeadLetterOutboxMessage> DeadLetterOutboxMessages { get; set; }
}
