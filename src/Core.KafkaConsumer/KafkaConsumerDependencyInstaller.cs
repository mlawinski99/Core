using Microsoft.Extensions.DependencyInjection;

namespace Core.KafkaConsumer;

public static class KafkaConsumerDependencyInstaller
{
    public static IServiceCollection AddKafkaConsumer(
        this IServiceCollection services)
    {
        services.AddScoped<IConsumer, KafkaConsumer>();

        return services;
    }
}