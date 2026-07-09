using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;

namespace Core.IntegrationTests.Shared;

public abstract class IntegrationTestBase<TFixture> : IDisposable
    where TFixture : IntegrationTestFixtureBase
{
    protected TFixture Fixture { get; }
    protected TestDbContext Db { get; }

    protected IntegrationTestBase(TFixture fixture)
    {
        Fixture = fixture;
        Db = CreateDbContext();
    }

    protected virtual TestDbContext CreateDbContext() => Fixture.CreateDbContext();

    public virtual void Dispose()
    {
        Db.Dispose();
    }
}
