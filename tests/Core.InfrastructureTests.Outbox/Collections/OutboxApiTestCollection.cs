using Core.InfrastructureTests.Outbox.Infrastructure;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Collections;

[CollectionDefinition("OutboxApi")]
public class OutboxApiTestCollection : ICollectionFixture<OutboxApiFixture>;