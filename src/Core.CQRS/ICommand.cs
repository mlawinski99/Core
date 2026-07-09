namespace Core.CQRS;

public interface ICommand<TResponse> : IRequest<TResponse>
{
    
}