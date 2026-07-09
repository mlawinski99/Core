using Core.DateTimeProvider;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Core.DataAccessTypes;

public static class DataAccessTypesDependencyInstaller
{
    public static IServiceCollection AddSharedDataAccessTypes(this IServiceCollection services)
    {
        services.AddDateProvider();
        services.AddScoped<IInterceptor, EncryptableInterceptor>();
        services.AddScoped<IInterceptor, VersionableInterceptor>();
        services.AddScoped<IInterceptor, AuditableInterceptor>();
        services.AddScoped<IInterceptor, SoftDeletableInterceptor>();

        return services;
    }
}