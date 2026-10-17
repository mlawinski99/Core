using Core.IntegrationTests.Permissions.Infrastructure;
using Xunit;

namespace Core.IntegrationTests.Permissions.Collections;

[CollectionDefinition("PermissionsApi")]
public class PermissionsApiTestCollection : ICollectionFixture<PermissionsApiFixture>;