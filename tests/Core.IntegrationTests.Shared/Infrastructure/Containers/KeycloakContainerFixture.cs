using Core.IntegrationTests.Shared.Settings;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Core.IntegrationTests.Shared.Infrastructure.Containers;

public class KeycloakContainerFixture : IAsyncDisposable
{
    private readonly IContainer _container;

    public string BaseUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}";
    public string Realm => "test-realm";
    public string ClientId => "test-client";
    public string ClientSecret => "test-secret";

    public KeycloakContainerFixture()
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

    public Task StartAsync() => _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}