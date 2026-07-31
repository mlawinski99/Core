using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS.Decorators;

public static class CqrsDecoratorsDependencyInstaller
{
    public static IServiceCollection AddCqrsDecorators(this IServiceCollection services)
    {
        services.RegisterCommandDecorators();
        services.RegisterQueryDecorators();

        return services;
    }

    private static void RegisterCommandDecorators(this IServiceCollection services)
    {
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ExceptionHandlingCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
    }

    private static void RegisterQueryDecorators(this IServiceCollection services)
    {
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ExceptionHandlingQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));
    }
}