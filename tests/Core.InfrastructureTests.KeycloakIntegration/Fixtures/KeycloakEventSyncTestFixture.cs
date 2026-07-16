using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.Containers;
using Core.Keycloak;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration.Fixtures;

public class KeycloakEventSyncTestFixture : IntegrationTestFixtureBase, IAsyncLifetime
{
    private readonly KeycloakContainerFixture _keycloakFixture = new();
    private readonly PostgresContainerFixture _postgresFixture = new("synctestdb");
    private readonly IHttpClientFactory _httpClientFactory = new TestHttpClientFactory();

    protected override string PostgresConnectionString => _postgresFixture.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _keycloakFixture.StartAsync(),
            _postgresFixture.StartAsync()
        );

        await InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public IKeycloakService CreateKeycloakService()
    {
        var config = Options.Create(new KeycloakConfig
        {
            AuthServerUrl = _keycloakFixture.BaseUrl,
            Realm = _keycloakFixture.Realm,
            ClientId = _keycloakFixture.ClientId,
            ClientSecret = _keycloakFixture.ClientSecret
        });

        return new KeycloakService(_httpClientFactory, config, new TestJsonSerializer());
    }

    public KeycloakConfig CreateKeycloakConfig()
    {
        return new KeycloakConfig
        {
            AuthServerUrl = _keycloakFixture.BaseUrl,
            Realm = _keycloakFixture.Realm,
            ClientId = _keycloakFixture.ClientId,
            ClientSecret = _keycloakFixture.ClientSecret
        };
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _keycloakFixture.DisposeAsync().AsTask(),
            _postgresFixture.DisposeAsync().AsTask()
        );
    }
}
