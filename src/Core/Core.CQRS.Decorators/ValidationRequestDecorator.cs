using Core.ResultPattern;
using Core.Validation;

namespace Core.CQRS.Decorators;

public class ValidationRequestDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> requestHandler,
    IEnumerable<IValidator<TRequest>> validators)
    : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult<TResult>
{
    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            errors.AddRange(result.Errors);
        }

        if (errors.Count > 0)
            return TResult.BadRequest(string.Join(", ", errors));

        return await requestHandler.Handle(request, cancellationToken);
    }
}