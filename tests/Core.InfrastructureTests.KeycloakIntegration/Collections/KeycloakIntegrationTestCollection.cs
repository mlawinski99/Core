using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[CollectionDefinition("KeycloakIntegration")]
public class KeycloakIntegrationTestCollection : ICollectionFixture<KeycloakIntegrationTestFixture>
{
}
