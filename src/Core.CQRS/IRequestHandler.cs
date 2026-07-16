namespace Core.CQRS;

public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest request, CancellationToken cancellationToken);
}
