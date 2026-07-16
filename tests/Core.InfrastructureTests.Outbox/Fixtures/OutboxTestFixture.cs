using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure.Containers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Fixtures;

public class OutboxTestFixture : IntegrationTestFixtureBase, IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgresFixture = new("outboxtestdb");

    protected override string PostgresConnectionString => _postgresFixture.ConnectionString;

    public TestOutboxDbContext CreateOutboxDbContext()
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        return new TestOutboxDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _postgresFixture.StartAsync();

        await using var context = CreateOutboxDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresFixture.DisposeAsync();
    }
}
