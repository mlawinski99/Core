using Core.Caching;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.Logger;
using Core.ResultPattern;
using Core.Validation;

namespace Core.UnitTests.CQRS.Decorators;

// fakes record the name of the decorator that calls them, so tests can assert the pipeline order

public record RecordingCommand : ICommand<Result>;

public class RecordingCommandHandler(List<string> calls) : ICommandHandler<RecordingCommand, Result>
{
    public Task<Result> Handle(RecordingCommand command, CancellationToken cancellationToken)
    {
        calls.Add(nameof(RecordingCommandHandler));
        return Task.FromResult(Result.Success);
    }
}

public record RecordingQuery : IQuery<Result<int>>, ICacheable
{
    public string CacheKey => "recording";
    public TimeSpan CacheExpiration => TimeSpan.FromMinutes(5);
}

public class RecordingQueryHandler(List<string> calls) : IQueryHandler<RecordingQuery, Result<int>>
{
    public Task<Result<int>> Handle(RecordingQuery query, CancellationToken cancellationToken)
    {
        calls.Add(nameof(RecordingQueryHandler));
        return Task.FromResult(Result<int>.Success(1));
    }
}

public class RecordingLogger<T>(List<string> calls) : IAppLogger<T>
{
    public void LogInformation(string message, params object[] args) => calls.Add(nameof(LoggingRequestDecorator<,>));
    public void LogWarning(string message, params object[] args) => calls.Add(nameof(LoggingRequestDecorator<,>));
    public void LogDebug(string message, params object[] args) => calls.Add(nameof(LoggingRequestDecorator<,>));
    public void LogError(string message, params object[] args) => calls.Add(nameof(LoggingRequestDecorator<,>));
    public void LogError(Exception exception, string message, params object[] args) => calls.Add(nameof(LoggingRequestDecorator<,>));
}

public class RecordingValidator<T>(List<string> calls) : IValidator<T>
{
    public Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        calls.Add(nameof(ValidationRequestDecorator<,>));
        return Task.FromResult(new ValidationResult());
    }
}

public class RecordingUnitOfWork(List<string> calls) : IUnitOfWork
{
    public void EnsureNoActiveTransaction(string commandName) { }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) where T : IResult<T>
    {
        calls.Add(nameof(TransactionCommandDecorator<,>));
        return operation(cancellationToken);
    }
}

public class RecordingCacheService(List<string> calls) : ICacheService
{
    public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default)
    {
        calls.Add(nameof(CachingQueryDecorator<,>));
        return Task.FromResult(default(T));
    }

    public Task Set<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<T> GetOrCreate<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) =>
        factory(cancellationToken);

    public Task Remove(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}