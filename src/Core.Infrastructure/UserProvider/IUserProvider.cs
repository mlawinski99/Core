using System.Security.Claims;

namespace Core.Infrastructure;

public interface IUserProvider
{
    Guid? UserId { get; }
}