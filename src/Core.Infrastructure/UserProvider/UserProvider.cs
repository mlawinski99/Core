using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Core.Infrastructure;

public class UserProvider(IHttpContextAccessor httpContextAccessor) : IUserProvider
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}