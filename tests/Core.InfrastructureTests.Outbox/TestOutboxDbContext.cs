using Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Core.InfrastructureTests.Outbox;

public class TestOutboxDbContext : DbContext, IOutbox
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options)
        : base(options)
    {
    }
}