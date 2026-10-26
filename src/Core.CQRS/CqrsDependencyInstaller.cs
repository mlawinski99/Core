using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.CQRS;

public static class CqrsDependencyInstaller
{
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddHandlers(assembly, typeof(ICommandHandler<,>));
        services.AddHandlers(assembly, typeof(IQueryHandler<,>));

        // each library registers its own handlers with AddCqrs, the dispatcher only once
        services.TryAddScoped<IRequestDispatcher, RequestDispatcher>();

        return services;
    }

    private static void AddHandlers(this IServiceCollection services, Assembly assembly, Type handlerType)
    {
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(handlerType).Where(t => !t.IsGenericTypeDefinition), publicOnly: false)
            .AsImplementedInterfaces(t => t.IsGenericType && t.GetGenericTypeDefinition() == handlerType)
            .WithScopedLifetime());
    }
}
