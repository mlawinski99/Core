using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Xunit;

namespace Core.IntegrationTests.Shared;

public abstract class IntegrationTestBase(PostgresFixture postgresFixture) : IAsyncLifetime
{
    protected PostgresFixture PostgresFixture { get; } = postgresFixture;

    protected TestDateTimeProvider DateTimeProvider { get; } = new();
    protected TestUserProvider UserProvider { get; } = new();
    protected TestEncryptor Encryptor { get; } = new();
    protected TestExpectedVersionProvider ExpectedVersionProvider { get; } = new();

    protected TestDbContext Db { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await PostgresFixture.EnsureSchemaCreatedAsync();

        Db = CreateDbContext();
    }

    protected virtual TestDbContext CreateDbContext() => PostgresFixture.CreateDbContext();

    public virtual async Task DisposeAsync()
    {
        if (Db is not null)
            await Db.DisposeAsync();
    }
}