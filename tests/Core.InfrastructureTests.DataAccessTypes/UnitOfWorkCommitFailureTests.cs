using System.Data.Common;
using Core.DataAccessTypes;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.ResultPattern;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class UnitOfWorkCommitFailureTests(PostgresFixture postgresFixture)
    : IntegrationTestBase(postgresFixture)
{
    protected override TestDbContext CreateDbContext() =>
        PostgresFixture.CreateDbContext(new FailingCommitInterceptor());

    [Fact]
    public async Task ExecuteInTransaction_WhenCommitFails_ShouldStopTrackingTheWrite()
    {
        // Arrange
        IUnitOfWork unitOfWork = Db;
        var entity = new TestEntity { Name = "CommitFails" };

        // Act
        var act = () => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            Db.TestEntities.Add(entity);
            await Db.SaveChangesAsync(ct);

            return Result.Success;
        });

        // Assert
        await act.Should().ThrowAsync<Exception>();

        Db.ChangeTracker.Entries().Should().BeEmpty();
    }
}

public class FailingCommitInterceptor : DbTransactionInterceptor
{
    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result) =>
        throw new InvalidOperationException();

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
}