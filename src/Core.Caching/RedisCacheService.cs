using Core.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Core.Caching;

public class RedisCacheService(
    IConnectionMultiplexer connectionMultiplexer,
    IJsonSerializer jsonSerializer,
    IOptions<RedisOptions> options)
    : ICacheService
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly RedisOptions _options = options.Value;

    public async Task<T?> Get<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (_, value) = await TryGet<T>(key);

        return value;
    }

    public async Task Set<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.StringSetAsync(
            EntryKey(key),
            jsonSerializer.Serialize(value),
            expiration ?? _options.DefaultExpiration);
    }

    public async Task<T> GetOrCreate<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (found, cached) = await TryGet<T>(key);
        if (found)
            return cached!;

        var created = await factory(cancellationToken);

        if (created is not null)
            await Set(key, created, expiration, cancellationToken);

        return created;
    }

    public async Task Remove(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.KeyDeleteAsync(EntryKey(key));
    }

    private async Task<(bool Found, T? Value)> TryGet<T>(string key)
    {
        var value = await _database.StringGetAsync(EntryKey(key));

        return value.IsNullOrEmpty
            ? (false, default)
            : (true, jsonSerializer.Deserialize<T>(value!));
    }

    private string EntryKey(string key) => $"{_options.KeyPrefix}:{key}";
}
