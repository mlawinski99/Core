using Core.Infrastructure.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure;

public static class InfrastructureDependencyInstaller
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // @TODO split into multiple dll
        services.Configure<AesEncryptorOptions>(configuration.GetSection(AesEncryptorOptions.SectionName));
        services.AddSingleton<IEncryptor, AesEncryptor>();
        services.AddScoped<IUserProvider, UserProvider>();
        services.AddScoped<IJsonSerializer, JsonSerializer>();

        return services;
    }
}