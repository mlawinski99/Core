using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration.Collections;

[CollectionDefinition("Keycloak")]
public class KeycloakTestCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<KeycloakFixture>;
