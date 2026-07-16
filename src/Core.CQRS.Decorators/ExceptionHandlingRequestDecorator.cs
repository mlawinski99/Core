using Core.DomainTypes;
using Core.Logger;
using Core.ResultPattern;

namespace Core.CQRS.Decorators;

public class ExceptionHandlingRequestDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> requestHandler,
    IAppLogger<ExceptionHandlingRequestDecorator<TRequest, TResult>> logger)
    : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult<TResult>
{
    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var handlerName = typeof(TRequest).Name;

        try
        {
            return await requestHandler.Handle(request, cancellationToken);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain rule violation in {Handler}: {Message}", handlerName, ex.Message);
            return TResult.UnprocessableEntity(ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Request {Handler} was cancelled", handlerName);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in handler {Handler}", handlerName);
            return TResult.InternalError();
        }
    }
}
