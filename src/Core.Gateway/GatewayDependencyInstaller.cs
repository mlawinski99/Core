using System.Threading.RateLimiting;
using Core.Extensions;
using Core.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Core.Gateway;

public static class GatewayDependencyInstaller
{
    public static IServiceCollection AddGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("Gateway"));

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
        var rateLimitSection = configuration.GetSection(RateLimitOptions.SectionName);

        services.AddOptions<RateLimitOptions>()
            .Bind(rateLimitSection)
            .Validate(o => o.PerUser.PermitLimit > 0, "RateLimit:PerUser:PermitLimit must be greater than zero")
            .Validate(o => o.PerUser.Window > TimeSpan.Zero, "RateLimit:PerUser:Window must be greater than zero")
            .Validate(_ => IsWrittenAsTimeSpan(rateLimitSection, nameof(RateLimitOptions.PerUser)),
                WindowFormatMessage(nameof(RateLimitOptions.PerUser)))
            .Validate(o => o.PerIpAddress.PermitLimit > 0, "RateLimit:PerIpAddress:PermitLimit must be greater than zero")
            .Validate(o => o.PerIpAddress.Window > TimeSpan.Zero, "RateLimit:PerIpAddress:Window must be greater than zero")
            .Validate(_ => IsWrittenAsTimeSpan(rateLimitSection, nameof(RateLimitOptions.PerIpAddress)),
                WindowFormatMessage(nameof(RateLimitOptions.PerIpAddress)))
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(GatewayPolicyNames.FixedRateLimit, httpContext =>
            {
                var rateLimitOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                var partition = GatewayRateLimitPartitioner.Resolve(httpContext, rateLimitOptions);

                return RateLimitPartition.GetFixedWindowLimiter(partition.Key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = partition.Window.PermitLimit,
                    Window = partition.Window.Window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    private static bool IsWrittenAsTimeSpan(IConfiguration rateLimitSection, string partition)
    {
        var window = rateLimitSection[$"{partition}:{nameof(RateLimitWindowOptions.Window)}"];

        // null - use default
        return window is null || window.IsTimeSpan();
    }

    private static string WindowFormatMessage(string partition) =>
        $"RateLimit:{partition}:Window must be written as a TimeSpan, for example \"00:01:00\" for one minute.";
}
