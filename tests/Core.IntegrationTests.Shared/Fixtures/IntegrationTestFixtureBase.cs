using Core.DateTimeProvider;
using Core.Infrastructure;
using Core.Infrastructure.Json;
using Core.IntegrationTests.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.IntegrationTests.Shared.Fixtures;

public class IntegrationTestFixtureBase
{
    protected virtual string PostgresConnectionString { get; set; } = string.Empty;

    public TestDateTimeProvider DateTimeProvider { get; } = new();
    public TestUserProvider UserProvider { get; } = new();

    public void SetUtcNow(DateTime utcNow) => DateTimeProvider.UtcNow = utcNow;
    public void SetUserId(Guid? userId) => UserProvider.UserId = userId;

    public virtual TestDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .EnableServiceProviderCaching(false)
            .Options;

        return new TestDbContext(options, new TestJsonSerializer(), interceptors);
    }
}
