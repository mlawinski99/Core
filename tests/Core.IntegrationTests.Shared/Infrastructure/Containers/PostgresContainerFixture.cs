using Core.IntegrationTests.Shared.Settings;
using Testcontainers.PostgreSql;

namespace Core.IntegrationTests.Shared.Infrastructure.Containers;

public class PostgresContainerFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public PostgresContainerFixture(string database = "testdb")
    {
        _container = new PostgreSqlBuilder()
            .WithImage(ContainerImages.Postgres)
            .WithDatabase(database)
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    public Task StartAsync() => _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
