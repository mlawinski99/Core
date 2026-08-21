using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("Outbox")]
public class OutboxInterceptorTests(PostgresFixture postgresFixture)
    : IntegrationTestBase(postgresFixture)
{
    protected override TestDbContext CreateDbContext() =>
        PostgresFixture.CreateDbContext(new OutboxInterceptor(new TestJsonSerializer()));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithAggregateDomainEvents_ShouldWriteOutboxMessagesAndClearEvents(bool useAsync)
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        entity.RaiseCreatedEvent();
        Db.TestEntities.Add(entity);

        // Act
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var message = await Db.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Content.Contains(entity.Id.ToString()));
        message.Should().NotBeNull();
        message.Type.Should().Be(typeof(TestEntityCreatedEvent).FullName);
        message.IsProcessed.Should().BeFalse();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChanges_WhenSaveFailsAndIsRetried_ShouldAddOutboxMessagesOnlyOnce()
    {
        // Arrange
        var existing = new TestEntity { Name = "Existing" };
        await using (var seedContext = PostgresFixture.CreateDbContext())
        {
            seedContext.TestEntities.Add(existing);
            await seedContext.SaveChangesAsync();
        }

        var entity = new TestEntity { Id = existing.Id, Name = "Test" };
        entity.RaiseCreatedEvent();
        Db.TestEntities.Add(entity);

        // Act
        var failedSave = () => Db.SaveChangesAsync();
        await failedSave.Should().ThrowAsync<DbUpdateException>();

        entity.DomainEvents.Should().BeEmpty();

        entity.Id = Guid.NewGuid();
        await Db.SaveChangesAsync();

        // Assert
        var messages = await Db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Content.Contains(existing.Id.ToString()))
            .ToListAsync();

        messages.Should().ContainSingle();
    }

    [Fact]
    public async Task SavingChanges_WithSecondSaveInSameTransaction_ShouldStageEachEventOnce()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        entity.RaiseCreatedEvent();
        Db.TestEntities.Add(entity);

        // Act
        await using var transaction = await Db.Database.BeginTransactionAsync();
        await Db.SaveChangesAsync();

        entity.Name = "Changed";
        await Db.SaveChangesAsync();

        await transaction.CommitAsync();

        // Assert
        var messages = await Db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Content.Contains(entity.Id.ToString()))
            .ToListAsync();

        messages.Should().ContainSingle();
    }
}
