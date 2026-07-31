using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS;

public class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    public async Task<TResult> Dispatch<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var handlerType = GetHandlerType(request);
        dynamic handler = scope.ServiceProvider.GetRequiredService(handlerType);
        return await handler.Handle((dynamic)request, cancellationToken);
    }

    private static Type GetHandlerType<TResult>(IRequest<TResult> request)
    {
        var handlerInterface = request switch
        {
            ICommand<TResult> => typeof(ICommandHandler<,>),
            IQuery<TResult> => typeof(IQueryHandler<,>),
            _ => throw new InvalidOperationException(
                $"{request.GetType().Name} must implement ICommand<TResult> or IQuery<TResult> to be dispatched.")
        };

        return handlerInterface.MakeGenericType(request.GetType(), typeof(TResult));
    }
}