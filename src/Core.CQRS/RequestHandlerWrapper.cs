using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS;

internal abstract class RequestHandlerWrapper<TResult>
{
    public abstract Task<TResult> Handle(
        IRequest<TResult> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class CommandHandlerWrapper<TCommand, TResult> : RequestHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    public override Task<TResult> Handle(
        IRequest<TResult> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return serviceProvider
            .GetRequiredService<ICommandHandler<TCommand, TResult>>()
            .Handle((TCommand)request, cancellationToken);
    }
}

internal sealed class QueryHandlerWrapper<TQuery, TResult> : RequestHandlerWrapper<TResult>
    where TQuery : IQuery<TResult>
{
    public override Task<TResult> Handle(
        IRequest<TResult> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResult>>()
            .Handle((TQuery)request, cancellationToken);
    }
}