using Microsoft.Extensions.DependencyInjection;

namespace Core.Validation;

public static class ValidationDependencyInstaller
{
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.AddTransient(typeof(IValidator<>), typeof(FluentValidationAdapter<>));
        return services;
    }
}