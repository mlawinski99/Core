namespace Core.Gateway;

public class RateLimitWindowOptions
{
    public int PermitLimit { get; init; } = 100;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}
