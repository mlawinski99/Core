using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Storage.Collections;

[CollectionDefinition("Storage")]
public class StorageTestCollection : ICollectionFixture<GarageFixture>, ICollectionFixture<PostgresFixture>;