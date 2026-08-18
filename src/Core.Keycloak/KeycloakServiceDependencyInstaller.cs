using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Core.Keycloak;

public static class KeycloakServiceDependencyInstaller
{
    public static IServiceCollection AddKeycloakService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KeycloakConfig>()
            .Bind(configuration.GetSection(KeycloakConfig.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.AuthServerUrl), "Keycloak:AuthServerUrl must not be empty")
            .Validate(o => Uri.IsWellFormedUriString(o.AuthServerUrl, UriKind.Absolute) &&
                           Uri.TryCreate(o.AuthServerUrl, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "Keycloak:AuthServerUrl must be an absolute http or https URI")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Realm), "Keycloak:Realm must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Keycloak:ClientId must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Keycloak:ClientSecret must not be empty")
            .ValidateOnStart();

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