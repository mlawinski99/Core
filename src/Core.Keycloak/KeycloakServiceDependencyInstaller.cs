using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Core.Keycloak;

public static class KeycloakServiceDependencyInstaller
{
    public static IServiceCollection AddKeycloakService(
        this IServiceCollection services)
    {
        services.AddScoped<IKeycloakService, KeycloakService>();

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                x => TimeSpan.FromSeconds(Math.Pow(2, x))
            );

        services.AddHttpClient(KeycloakEndpoints.HttpClientName)
            .AddPolicyHandler(policy);

        return services;
    }
}
