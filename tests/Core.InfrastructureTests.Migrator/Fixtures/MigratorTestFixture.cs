using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure.Containers;
using Xunit;

namespace Core.InfrastructureTests.Migrator.Fixtures;

public class MigratorTestFixture : IntegrationTestFixtureBase, IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgresFixture = new("migratortestdb");

    protected override string PostgresConnectionString => _postgresFixture.ConnectionString;

    public string ConnectionString => PostgresConnectionString;

    public Core.Migrator.Migrator CreateMigrator(string scriptPath) =>
        new(PostgresConnectionString, scriptPath);

    public async Task InitializeAsync()
    {
        await _postgresFixture.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresFixture.DisposeAsync();
    }
}