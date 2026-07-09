using Core.InfrastructureTests.Outbox.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Collections;

[CollectionDefinition("Outbox")]
public class OutboxTestCollection : ICollectionFixture<OutboxTestFixture>;