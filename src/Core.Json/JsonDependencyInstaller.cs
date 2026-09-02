using Microsoft.Extensions.DependencyInjection;

namespace Core.Json;

public static class JsonDependencyInstaller
{
    public static IServiceCollection AddJson(this IServiceCollection services)
    {
        services.AddScoped<IJsonSerializer, JsonSerializer>();

        return services;
    }
}