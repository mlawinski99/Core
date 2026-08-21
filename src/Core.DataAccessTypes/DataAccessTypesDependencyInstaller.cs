using Core.DateTimeProvider;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Core.DataAccessTypes;

public static class DataAccessTypesDependencyInstaller
{
    public static IServiceCollection AddSharedDataAccessTypes(this IServiceCollection services)
    {
        services.AddDateProvider();
        services.AddTransient<IInterceptor, EncryptableInterceptor>();
        services.AddTransient<IInterceptor, VersionableInterceptor>();
        services.AddTransient<IInterceptor, AuditableInterceptor>();
        services.AddTransient<IInterceptor, SoftDeletableInterceptor>();

        return services;
    }

    public static IServiceCollection AddUnitOfWork<TContext>(this IServiceCollection services)
        where TContext : BaseDbContext
    {
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
