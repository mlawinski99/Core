namespace Core.Gateway;

public record GatewayRateLimitPartition(string Key, RateLimitWindowOptions Window);