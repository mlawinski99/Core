using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS.Decorators;

public static class CqrsDecoratorsDependencyInstaller
{
    public static IServiceCollection AddCqrsDecorators(this IServiceCollection services)
    {
        services.Decorate(typeof(IRequestHandler<,>), typeof(ValidationRequestDecorator<,>));
        services.Decorate(typeof(IRequestHandler<,>), typeof(ExceptionHandlingRequestDecorator<,>));
        services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingRequestDecorator<,>));

        return services;
    }
}
