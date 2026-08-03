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

        activity?.SetTag("cqrs.result_code", result?.Code.ToString());
        logger.LogInformation("{Handler} completed with {ResultCode} in {ElapsedMs}ms", handlerName, result?.Code, sw.ElapsedMilliseconds);

        return result;
    }
}

public sealed class LoggingCommandDecorator<TCommand, TResult>(
    IRequestHandler<TCommand, TResult> requestHandler,
    IAppLogger<LoggingRequestDecorator<TCommand, TResult>> logger)
    : LoggingRequestDecorator<TCommand, TResult>(requestHandler, logger),
        ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : IResult<TResult>;

public sealed class LoggingQueryDecorator<TQuery, TResult>(
    IRequestHandler<TQuery, TResult> requestHandler,
    IAppLogger<LoggingRequestDecorator<TQuery, TResult>> logger)
    : LoggingRequestDecorator<TQuery, TResult>(requestHandler, logger),
        IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : IResult<TResult>;
