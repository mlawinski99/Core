using FluentValidation;

namespace Core.Validation;

public class FluentValidationAdapter<T>(
    IEnumerable<FluentValidation.IValidator<T>> validators) : IValidator<T>
{
    public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<T>(instance), cancellationToken);
            errors.AddRange(result.Errors
                .Where(e => e is not null)
                .Select(e => e.ErrorMessage));
        }

        return new ValidationResult { Errors = errors };
    }
}
