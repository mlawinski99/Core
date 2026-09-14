using Core.CQRS;
using Core.ResultPattern;

namespace Core.UnitTests.CQRS.Decorators;

public record TestQuery(int Number) : IQuery<Result<int>>;

public record CacheableTestQuery(int Number) : IQuery<Result<int>>, ICacheable
{
    public string CacheKey => Number.ToString();
    public TimeSpan CacheExpiration => TimeSpan.FromMinutes(5);
}

public class TestQueryHandler : IQueryHandler<TestQuery, Result<int>>
{
    public Task<Result<int>> Handle(TestQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<int>.Success(query.Number * 2));
    }
}

public class FailingQueryHandler : IQueryHandler<TestQuery, Result<int>>
{
    public Task<Result<int>> Handle(TestQuery query, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Query failed");
    }
}
