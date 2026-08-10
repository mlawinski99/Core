using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Migrator.Collections;

[CollectionDefinition("Migrator")]
public class MigratorTestCollection : ICollectionFixture<PostgresFixture>;