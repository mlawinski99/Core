using Core.Infrastructure.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure;

public static class InfrastructureDependencyInstaller
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // @TODO split into multiple dll
        services.AddOptions<AesEncryptorOptions>()
            .Bind(configuration.GetSection(AesEncryptorOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key), "Encryption:Key must not be empty")
            .ValidateOnStart();

        services.AddSingleton<IEncryptor, AesEncryptor>();
        services.AddScoped<IUserProvider, UserProvider>();
        services.AddScoped<IExpectedVersionProvider, ExpectedVersionProvider>();
        services.AddScoped<IJsonSerializer, JsonSerializer>();

        return services;
    }
}
