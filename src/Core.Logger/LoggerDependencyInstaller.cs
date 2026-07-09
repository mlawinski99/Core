using Microsoft.Extensions.DependencyInjection;

namespace Core.Logger;

public static class LoggerDependencyInstaller
{
    public static IServiceCollection AddAppLogger(
        this IServiceCollection services)
    {
        services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));

        return services;
    }
}