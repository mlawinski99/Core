using Core.InfrastructureTests.Migrator.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Migrator.Collections;

[CollectionDefinition("Migrator")]
public class MigratorTestCollection : ICollectionFixture<MigratorTestFixture>;