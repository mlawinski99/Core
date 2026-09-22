using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Core.Gateway;

public static class GatewayRateLimitPartitioner
{
    public static GatewayRateLimitPartition Resolve(HttpContext httpContext, RateLimitOptions options)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return new GatewayRateLimitPartition($"user:{userId}", options.PerUser);
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress
            ?? throw new InvalidOperationException(
                "Cannot rate limit an anonymous request: the connection has no remote IP address.");

        return new GatewayRateLimitPartition($"ip:{ipAddress}", options.PerIpAddress);
    }
}
