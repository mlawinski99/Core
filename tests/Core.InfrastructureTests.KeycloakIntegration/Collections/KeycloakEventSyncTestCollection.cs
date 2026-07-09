using Core.InfrastructureTests.KeycloakIntegration.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[CollectionDefinition("KeycloakEventSync")]
public class KeycloakEventSyncTestCollection : ICollectionFixture<KeycloakEventSyncTestFixture>
{
}
