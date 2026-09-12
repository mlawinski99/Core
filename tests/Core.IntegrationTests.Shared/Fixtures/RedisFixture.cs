using Core.IntegrationTests.Shared.Settings;
using Testcontainers.Redis;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage(ContainerImages.Redis)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}