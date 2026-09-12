namespace Core.Caching;

public interface ICacheService
{
    Task<T?> Get<T>(string key, CancellationToken cancellationToken = default);

    Task Set<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task<T> GetOrCreate<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task Remove(string key, CancellationToken cancellationToken = default);
}