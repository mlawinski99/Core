using Core.IntegrationTests.Shared.Infrastructure.Containers;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class IntegrationTestFixture : IntegrationTestFixtureBase, IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgresFixture = new();

    protected override string PostgresConnectionString => _postgresFixture.ConnectionString;

    public async Task InitializeAsync()
    {
        await _postgresFixture.StartAsync();
        await InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresFixture.DisposeAsync();
    }
}
