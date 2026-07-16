using System.Diagnostics;
using Core.Logger;
using Core.ResultPattern;

namespace Core.CQRS.Decorators;

public class LoggingRequestDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> requestHandler,
    IAppLogger<LoggingRequestDecorator<TRequest, TResult>> logger)
    : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult<TResult>
{
    private static readonly ActivitySource ActivitySource = new("Core.CQRS");

    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var handlerName = typeof(TRequest).Name;

        using var activity = ActivitySource.StartActivity(handlerName, ActivityKind.Internal);
        activity?.SetTag("cqrs.handler", handlerName);

        var sw = Stopwatch.StartNew();
        logger.LogInformation("Executing {Handler}", handlerName);

        var result = await requestHandler.Handle(request, cancellationToken);
        sw.Stop();

        if (result is Result r)
        {
            activity?.SetTag("cqrs.result_code", r.Code.ToString());
            logger.LogInformation("{Handler} completed with {ResultCode} in {ElapsedMs}ms", handlerName, r.Code, sw.ElapsedMilliseconds);
        }

        return result;
    }
}
