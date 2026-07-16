using Core.DataAccessTypes;
using Core.Identity.Context;
using Microsoft.Extensions.DependencyInjection;

namespace Core.KeycloakSync;

public static class KeycloakSyncDependencyInstaller
{
    public static IServiceCollection AddKeycloakUserSync<TContext>(
        this IServiceCollection services) where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
    {
        services.AddScoped<KeycloakEventImporter<TContext>>();
        services.AddScoped<KeycloakEventProcessor<TContext>>();
        services.AddScoped<KeycloakUserSyncJob<TContext>>();

        return services;
    }
}
