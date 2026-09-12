using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Core.Caching;

public static class CachingDependencyInstaller
{
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Redis:ConnectionString must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.KeyPrefix), "Redis:KeyPrefix must not be empty")
            .Validate(o => o.DefaultExpiration > TimeSpan.Zero, "Redis:DefaultExpiration must be greater than zero")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(sp.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}