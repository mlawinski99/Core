using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Caching.Collections;

[CollectionDefinition("Caching")]
public class CachingTestCollection : ICollectionFixture<RedisFixture>;