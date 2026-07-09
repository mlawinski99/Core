using Microsoft.Extensions.DependencyInjection;

namespace Core.Outbox;

public static class OutboxDependencyInstaller
{
    public static IServiceCollection AddOutbox(
        this IServiceCollection services)
    {
        services.AddScoped(typeof(IOutboxMessageProcessor<>), typeof(OutboxMessageProcessor<>));

        return services;
    }
}