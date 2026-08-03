namespace Core.CQRS;

// skip transactional decorator
public interface INonTransactionalCommand<TResponse> : ICommand<TResponse>;
