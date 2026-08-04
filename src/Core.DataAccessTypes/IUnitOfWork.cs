using Core.ResultPattern;

namespace Core.DataAccessTypes;

public interface IUnitOfWork
{
    void EnsureNoActiveTransaction(string commandName);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) where T : IResult<T>;
}
