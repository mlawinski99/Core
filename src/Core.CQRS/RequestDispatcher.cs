using System.Collections.Concurrent;

namespace Core.CQRS;

public class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public async Task<TResult> Dispatch<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        var wrapper = (RequestHandlerWrapper<TResult>)Wrappers.GetOrAdd(
            request.GetType(),
            static (_, r) => CreateWrapper(r),
            request);

        return await wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private static object CreateWrapper<TResult>(IRequest<TResult> request)
    {
        var wrapperType = request switch
        {
            ICommand<TResult> => typeof(CommandHandlerWrapper<,>),
            IQuery<TResult> => typeof(QueryHandlerWrapper<,>),
            _ => throw new InvalidOperationException(
                $"{request.GetType().Name} must implement ICommand<TResult> or IQuery<TResult> to be dispatched.")
        };

        return Activator.CreateInstance(wrapperType.MakeGenericType(request.GetType(), typeof(TResult)))!;
    }
}
