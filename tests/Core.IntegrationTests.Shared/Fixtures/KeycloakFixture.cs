using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Settings;
using Core.Keycloak;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class KeycloakFixture : IAsyncLifetime
{
    private readonly IContainer _container;
    private readonly IHttpClientFactory _httpClientFactory = new TestHttpClientFactory();

    public string BaseUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}";
    public string Realm => "test-realm";
    public string ClientId => "test-client";
    public string ClientSecret => "test-secret";

    public KeycloakFixture()
    {
        var realmDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        var realmPath = Path.Combine(realmDir, "test-realm.json");

        if (!File.Exists(realmPath))
            throw new FileNotFoundException($"Keycloak realm file not found at: {realmPath}");

        _container = new ContainerBuilder()
            .WithImage(ContainerImages.Keycloak)
            .WithPortBinding(8080, true)
            .WithResourceMapping(realmDir, "/opt/keycloak/data/import")
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithCommand("start-dev", "--import-realm")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath($"/realms/{Realm}")
                    .ForPort(8080)))
            .Build();
    }

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public KeycloakConfig CreateKeycloakConfig() => new()
    {
        AuthServerUrl = BaseUrl,
        Realm = Realm,
        ClientId = ClientId,
        ClientSecret = ClientSecret
    };

    public IKeycloakService CreateKeycloakService() =>
        new KeycloakService(_httpClientFactory, Options.Create(CreateKeycloakConfig()), new TestJsonSerializer());
}