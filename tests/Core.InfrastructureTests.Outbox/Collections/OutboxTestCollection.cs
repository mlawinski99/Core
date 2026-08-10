using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Collections;

[CollectionDefinition("Outbox")]
public class OutboxTestCollection : ICollectionFixture<PostgresFixture>;