using Microsoft.Extensions.DependencyInjection;

namespace Core.DateTimeProvider;

public static class DateTimeProviderDependencyInstaller
{
    public static IServiceCollection AddDateProvider(this IServiceCollection services)
    {
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}