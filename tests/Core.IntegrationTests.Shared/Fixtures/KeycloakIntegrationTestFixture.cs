using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.Containers;
using Core.Keycloak;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class KeycloakIntegrationTestFixture : IntegrationTestFixtureBase, IAsyncLifetime
{
    private readonly KeycloakContainerFixture _keycloakFixture = new();
    private readonly PostgresContainerFixture _postgresFixture = new();
    private readonly IHttpClientFactory _httpClientFactory = new TestHttpClientFactory();

    protected override string PostgresConnectionString => _postgresFixture.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _keycloakFixture.StartAsync(),
            _postgresFixture.StartAsync()
        );

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

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _keycloakFixture.DisposeAsync().AsTask(),
            _postgresFixture.DisposeAsync().AsTask()
        );
    }
}
