using Microsoft.Extensions.DependencyInjection;

namespace Core.RequestContext;

public static class RequestContextDependencyInstaller
{
    public static IServiceCollection AddRequestContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserProvider, UserProvider>();
        services.AddScoped<IExpectedVersionProvider, ExpectedVersionProvider>();

        return services;
    }
}