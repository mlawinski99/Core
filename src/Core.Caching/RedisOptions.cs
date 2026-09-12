namespace Core.Caching;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public required string ConnectionString { get; init; }
    public required string KeyPrefix { get; init; }
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromMinutes(5);
}