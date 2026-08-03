using Core.DataAccessTypes;
using Core.ResultPattern;

namespace Core.IntegrationTests.Shared.Infrastructure;

// for fixtures with no DbContext
public class NoDbUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) where T : IResult<T>
    {
        return operation(cancellationToken);
    }
}