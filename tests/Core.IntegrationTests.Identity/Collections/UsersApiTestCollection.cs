using Core.IntegrationTests.Identity.Infrastructure;
using Xunit;

namespace Core.IntegrationTests.Identity.Collections;

[CollectionDefinition("UsersApi")]
public class UsersApiTestCollection : ICollectionFixture<UsersApiFixture>;
