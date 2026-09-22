namespace Core.Gateway;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public RateLimitWindowOptions PerUser { get; init; } = new();
    public RateLimitWindowOptions PerIpAddress { get; init; } = new();
}
