using Core.DataAccessTypes;
using Core.RequestContext;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestUserProvider : IUserProvider
{
    public Guid? UserId { get; set; }
}
