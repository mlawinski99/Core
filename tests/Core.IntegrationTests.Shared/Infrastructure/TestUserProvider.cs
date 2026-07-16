using Core.DataAccessTypes;
using Core.Infrastructure;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestUserProvider : IUserProvider
{
    public Guid? UserId { get; set; }
}
