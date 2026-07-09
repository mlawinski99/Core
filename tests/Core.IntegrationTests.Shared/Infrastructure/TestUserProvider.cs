using Core.Infrastructure;
using Core.DataAccessTypes;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestUserProvider : IUserProvider
{
    public Guid? UserId { get; set; }
}
