using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.KafkaProducer;

public static class KafkaProducerDependencyInstaller
{
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaProducerConfiguration>()
            .Bind(configuration.GetSection(KafkaProducerConfiguration.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers),
                "Kafka:Producer:BootstrapServers must not be empty")
            .Validate(o => o.MessageTimeoutMs > 0, "Kafka:Producer:MessageTimeoutMs must be greater than 0")
            .ValidateOnStart();

        services.AddSingleton(typeof(IProducer<>), typeof(KafkaProducer<>));

        return services;
    }
}