using Microsoft.Extensions.DependencyInjection;

namespace Core.KafkaProducer;

public static class KafkaProducerDependencyInstaller
{
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services)
    {
        services.AddScoped(typeof(IProducer<>), typeof(KafkaProducer<>));

        return services;
    }
}