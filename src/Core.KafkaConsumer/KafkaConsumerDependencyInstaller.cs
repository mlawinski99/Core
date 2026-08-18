using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.KafkaConsumer;

public static class KafkaConsumerDependencyInstaller
{
    private static readonly string[] AllowedAutoOffsetResets = ["earliest", "latest"];

    public static IServiceCollection AddKafkaConsumer(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaConsumerConfiguration>()
            .Bind(configuration.GetSection(KafkaConsumerConfiguration.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers),
                "Kafka:Consumer:BootstrapServers must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka:Consumer:GroupId must not be empty")
            .Validate(o => o.AllowedTopics is { Count: > 0 }, "Kafka:Consumer:AllowedTopics must not be empty")
            .Validate(o => AllowedAutoOffsetResets.Contains(o.AutoOffsetReset.ToLowerInvariant()),
                "Kafka:Consumer:AutoOffsetReset must be one of earliest, latest")
            .ValidateOnStart();

        services.AddScoped<IConsumer, KafkaConsumer>();

        return services;
    }
}
