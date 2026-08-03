using Core.DataAccessTypes;
using Core.ResultPattern;

namespace Core.UnitTests.CQRS.Decorators;

public class TestUnitOfWork : IUnitOfWork
{
    public bool TransactionUsed { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) where T : IResult<T>
    {
        TransactionUsed = true;

        return await operation(cancellationToken);
    }
}