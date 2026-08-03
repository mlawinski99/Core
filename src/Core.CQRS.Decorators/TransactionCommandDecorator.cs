using Core.DataAccessTypes;
using Core.ResultPattern;

namespace Core.CQRS.Decorators;

public sealed class TransactionCommandDecorator<TCommand, TResult>(
    IRequestHandler<TCommand, TResult> requestHandler,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : IResult<TResult>
{
    private static readonly bool SkipTransaction =
        typeof(INonTransactionalCommand<TResult>).IsAssignableFrom(typeof(TCommand));

    public Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        if (SkipTransaction)
            return requestHandler.Handle(request, cancellationToken);

        return unitOfWork.ExecuteInTransactionAsync(
            ct => requestHandler.Handle(request, ct),
            cancellationToken);
    }
}