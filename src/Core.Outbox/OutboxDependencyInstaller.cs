using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Outbox;

public static class OutboxDependencyInstaller
{
    public static IServiceCollection AddOutbox(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(o => o.BatchSize > 0, "Outbox:BatchSize must be greater than 0")
            .Validate(o => o.MaxRetries > 0, "Outbox:MaxRetries must be greater than 0")
            .Validate(o => o.RetryBackoffBase >= 1, "Outbox:RetryBackoffBase must be at least 1")
            .Validate(o => o.MoveAfterDays >= 0, "Outbox:MoveAfterDays must not be negative")
            .Validate(o => o.MoveBatchSize > 0, "Outbox:MoveBatchSize must be greater than 0")
            .ValidateOnStart();

        services.AddScoped(typeof(IOutboxMessageProcessor<>), typeof(OutboxMessageProcessor<>));
        services.AddScoped(typeof(MoveProcessedOutboxMessagesJob<>));

        return services;
    }
}