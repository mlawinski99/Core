using Core.Caching;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace Core.InfrastructureTests.Caching;

[Collection("Caching")]
public class RedisCacheServiceTests(RedisFixture redisFixture) : IAsyncLifetime
{
    private readonly RedisOptions _options = new()
    {
        ConnectionString = redisFixture.ConnectionString,
        KeyPrefix = "core-tests",
        DefaultExpiration = TimeSpan.FromMinutes(10)
    };
    private readonly string _key = Guid.NewGuid().ToString();

    private IConnectionMultiplexer _connectionMultiplexer = null!;
    private RedisCacheService _cacheService = null!;

    public async Task InitializeAsync()
    {
        _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString);

        _cacheService = new RedisCacheService(
            _connectionMultiplexer,
            new TestJsonSerializer(),
            Options.Create(_options));
    }

    public async Task DisposeAsync() => await _connectionMultiplexer.DisposeAsync();

    [Fact]
    public async Task Get_WithStoredValue_ShouldReturnIt()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("stored", 7));

        // Act
        var value = await _cacheService.Get<TestCacheValue>(_key);

        // Assert
        value.Should().Be(new TestCacheValue("stored", 7));
    }

    [Fact]
    public async Task Get_WithMissingKey_ShouldReturnNull()
    {
        // Act
        var value = await _cacheService.Get<TestCacheValue>(_key);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public async Task Set_ShouldStoreUnderConfiguredKeyPrefix()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("prefixed", 1));

        // Act
        var exists = await _connectionMultiplexer.GetDatabase().KeyExistsAsync($"core-tests:{_key}");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Set_WithoutExpiration_ShouldApplyDefaultExpiration()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("default-ttl", 1));

        // Act
        var timeToLive = await _connectionMultiplexer.GetDatabase().KeyTimeToLiveAsync($"core-tests:{_key}");

        // Assert
        timeToLive.Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Set_WithExpiration_ShouldApplyGivenExpiration()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("explicit-ttl", 1), TimeSpan.FromMinutes(3));

        // Act
        var timeToLive = await _connectionMultiplexer.GetDatabase().KeyTimeToLiveAsync($"core-tests:{_key}");

        // Assert
        timeToLive.Should().BeCloseTo(TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Remove_ShouldDeleteEntry()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("removed", 1));

        // Act
        await _cacheService.Remove(_key);

        // Assert
        var value = await _cacheService.Get<TestCacheValue>(_key);
        value.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreate_WithMissingKey_ShouldSetKey()
    {
        // Act
        var created = await _cacheService.GetOrCreate(_key, _ => Task.FromResult(new TestCacheValue("created", 3)));

        // Assert
        created.Should().Be(new TestCacheValue("created", 3));

        var cached = await _cacheService.Get<TestCacheValue>(_key);
        cached.Should().Be(new TestCacheValue("created", 3));
    }

    [Fact]
    public async Task GetOrCreate_WithCachedValue_ShouldNotSet()
    {
        // Arrange
        await _cacheService.Set(_key, new TestCacheValue("exist", 9));

        // Act
        var value = await _cacheService.GetOrCreate(_key, _ => Task.FromResult(new TestCacheValue("exist", 0)));

        // Assert
        value.Should().Be(new TestCacheValue("exist", 9));
    }

    [Fact]
    public async Task GetOrCreate_WithNullFromFactory_ShouldNotCacheIt()
    {
        // Arrange
        var created = await _cacheService.GetOrCreate<TestCacheValue?>(_key, _ => Task.FromResult<TestCacheValue?>(null));

        // Act
        var exists = await _connectionMultiplexer.GetDatabase().KeyExistsAsync($"core-tests:{_key}");

        // Assert
        created.Should().BeNull();
        exists.Should().BeFalse();
    }
}
