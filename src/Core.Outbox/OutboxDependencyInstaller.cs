using Core.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Outbox;

public static class OutboxDependencyInstaller
{
    public static IServiceCollection AddOutbox<TContext>(
        this IServiceCollection services, IConfiguration configuration) where TContext : DbContext, IOutbox
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(o => o.BatchSize > 0, "Outbox:BatchSize must be greater than 0")
            .Validate(o => o.MaxRetries > 0, "Outbox:MaxRetries must be greater than 0")
            .Validate(o => o.RetryBackoffBase >= 1, "Outbox:RetryBackoffBase must be at least 1")
            .Validate(o => o.MoveAfterDays >= 0, "Outbox:MoveAfterDays must not be negative")
            .Validate(o => o.MoveBatchSize > 0, "Outbox:MoveBatchSize must be greater than 0")
            .Validate(o => CronValidator.IsValid(o.ProcessCron), "Outbox:ProcessCron must be a valid cron expression")
            .Validate(o => CronValidator.IsValid(o.MoveProcessedCron), "Outbox:MoveProcessedCron must be a valid cron expression")
            .ValidateOnStart();

        services.AddTransient<IInterceptor, OutboxInterceptor>();

        services.AddRecurringJob<ProcessOutboxMessagesJob<TContext>>(
            ProcessOutboxMessagesJob<TContext>.JobId, sp => sp.GetRequiredService<IOptions<OutboxOptions>>().Value.ProcessCron);
        services.AddRecurringJob<MoveProcessedOutboxMessagesJob<TContext>>(
            MoveProcessedOutboxMessagesJob<TContext>.JobId, sp => sp.GetRequiredService<IOptions<OutboxOptions>>().Value.MoveProcessedCron);

        return services;
    }
}
