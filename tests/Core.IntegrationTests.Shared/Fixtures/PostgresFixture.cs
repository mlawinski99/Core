using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage(ContainerImages.Postgres)
        .WithDatabase("testdb")
        .WithUsername("testuser")
        .WithPassword("testpassword")
        .Build();

    private Task? _schemaCreation;

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public TestDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TestDbContext(options, new TestJsonSerializer(), interceptors);
    }

    // for migrator tests
    public Task EnsureSchemaCreatedAsync() => _schemaCreation ??= CreateSchemaAsync();

    private async Task CreateSchemaAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }
}