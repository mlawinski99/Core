namespace Core.CQRS;

public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheExpiration { get; }
}
