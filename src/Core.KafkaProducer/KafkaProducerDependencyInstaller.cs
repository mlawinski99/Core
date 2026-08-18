using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.KafkaProducer;

public static class KafkaProducerDependencyInstaller
{
    private static readonly string[] AllowedAcks = ["none", "leader", "all"];

    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaProducerConfiguration>()
            .Bind(configuration.GetSection(KafkaProducerConfiguration.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers),
                "Kafka:Producer:BootstrapServers must not be empty")
            .Validate(o => o.MessageTimeoutMs > 0, "Kafka:Producer:MessageTimeoutMs must be greater than 0")
            .Validate(o => AllowedAcks.Contains(o.Acks.ToLowerInvariant()),
                "Kafka:Producer:Acks must be one of none, leader, all")
            .Validate(o => !o.EnableIdempotence || string.Equals(o.Acks, "all", StringComparison.OrdinalIgnoreCase),
                "Kafka:Producer:Acks must be all when Kafka:Producer:EnableIdempotence is true")
            .ValidateOnStart();

        services.AddScoped(typeof(IProducer<>), typeof(KafkaProducer<>));

        return services;
    }
}
