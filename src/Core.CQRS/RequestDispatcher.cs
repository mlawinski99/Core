using Microsoft.Extensions.DependencyInjection;

namespace Core.CQRS;

public class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    public async Task<TResult> Dispatch<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResult));
        dynamic handler = scope.ServiceProvider.GetRequiredService(handlerType);
        return await handler.Handle((dynamic)request, cancellationToken);
    }
}
