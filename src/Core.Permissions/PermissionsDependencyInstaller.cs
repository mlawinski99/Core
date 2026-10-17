using Core.DataAccessTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Permissions;

public static class PermissionsDependencyInstaller
{
    public static IServiceCollection AddPermissions<TContext>(this IServiceCollection services)
        where TContext : BaseDbContext, IPermissionContext
    {
        services.AddScoped<IPermissionContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
