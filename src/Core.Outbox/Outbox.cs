using Microsoft.EntityFrameworkCore;

namespace Core.Outbox;

public interface IOutbox
{
    DbSet<OutboxMessage> OutboxMessages { get; set; }
}