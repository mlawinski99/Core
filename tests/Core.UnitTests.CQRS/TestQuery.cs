using Core.CQRS;

namespace Core.UnitTests.CQRS;

public class TestQuery : IQuery<int>
{
    public int Number { get; init; }
}

public class TestQueryHandler : IQueryHandler<TestQuery, int>
{
    public Task<int> Handle(TestQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.Number * 2);
    }
}