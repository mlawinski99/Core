using Core.BackgroundJobs;
using Core.DataAccessTypes;
using Core.Identity.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.KeycloakSync;

public static class KeycloakSyncDependencyInstaller
{
    public static IServiceCollection AddKeycloakUserSync<TContext>(
        this IServiceCollection services, IConfiguration configuration)
        where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
    {
        services.AddOptions<KeycloakSyncOptions>()
            .Bind(configuration.GetSection(KeycloakSyncOptions.SectionName))
            .Validate(o => CronValidator.IsValid(o.Cron), "KeycloakSync:Cron must be a valid cron expression")
            .ValidateOnStart();

        services.AddScoped<KeycloakEventImporter<TContext>>();
        services.AddScoped<KeycloakEventProcessor<TContext>>();

        services.AddRecurringJob<KeycloakUserSyncJob<TContext>>(KeycloakUserSyncJob<TContext>.JobId, sp => sp.GetRequiredService<IOptions<KeycloakSyncOptions>>().Value.Cron);

        return services;
    }
}
