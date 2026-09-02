using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Encryption;

public static class EncryptionDependencyInstaller
{
    public static IServiceCollection AddEncryption(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AesEncryptorOptions>()
            .Bind(configuration.GetSection(AesEncryptorOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key), "Encryption:Key must not be empty")
            .ValidateOnStart();

        services.AddSingleton<IEncryptor, AesEncryptor>();

        return services;
    }
}