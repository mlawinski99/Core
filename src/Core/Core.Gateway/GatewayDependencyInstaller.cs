using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Core.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Core.Gateway;

public static class GatewayDependencyInstaller
{
    public static IServiceCollection AddGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("Gateaway"));

        services.AddGatewayAuthentication(configuration);
        services.AddGatewayRateLimiting(configuration);

        return services;
    }

    public static WebApplication UseGateway(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapReverseProxy();

        return app;
    }

    private static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakSection = configuration.GetSection("Keycloak");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakSection["Authority"];
                options.Audience = keycloakSection["Audience"];

                options.RequireHttpsMetadata = !string.Equals(
                    keycloakSection["RequireHttpsMetadata"], "false", StringComparison.OrdinalIgnoreCase);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(GatewayPolicyNames.Authenticated, policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }

    private static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitSection = configuration.GetSection("RateLimit");
        var permitLimit = rateLimitSection.GetValue("PermitLimit", 100);
        var windowSeconds = rateLimitSection.GetValue("WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter(GatewayPolicyNames.FixedRateLimit, limiterOptions =>
            {
                limiterOptions.PermitLimit = permitLimit;
                limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
        });

        return services;
    }
}