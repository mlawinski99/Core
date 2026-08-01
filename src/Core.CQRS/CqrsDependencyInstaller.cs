using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS;

public static class CqrsDependencyInstaller
{
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddHandlers(assembly, typeof(ICommandHandler<,>));
        services.AddHandlers(assembly, typeof(IQueryHandler<,>));

        services.AddScoped<IRequestDispatcher, RequestDispatcher>();

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
