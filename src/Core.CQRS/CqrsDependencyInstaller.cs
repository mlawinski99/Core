using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS;

public static class CqrsDependencyInstaller
{
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddSingleton<IRequestDispatcher, RequestDispatcher>();

        return services;
    }
}
