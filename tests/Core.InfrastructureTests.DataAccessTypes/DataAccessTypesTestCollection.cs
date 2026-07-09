using Core.IntegrationTests.Shared.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[CollectionDefinition("DataAccessTypesTest")]
public class DataAccessTypesTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
