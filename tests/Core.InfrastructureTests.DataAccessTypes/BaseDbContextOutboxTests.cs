using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class BaseDbContextOutboxTests(IntegrationTestFixture fixture)
    : IntegrationTestBase<IntegrationTestFixture>(fixture)
{
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
        var message = await GetOutboxMessageAsync(entity.Id);
        message.Should().NotBeNull();
        message!.Type.Should().Be(typeof(TestEntityCreatedEvent).FullName);
        message.IsProcessed.Should().BeFalse();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChanges_WithAcceptAllChangesOnSuccessOverload_ShouldWriteOutboxMessages()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        entity.RaiseCreatedEvent();
        Db.TestEntities.Add(entity);

        // Act
        await Db.SaveChangesAsync(acceptAllChangesOnSuccess: true);

        // Assert
        var message = await GetOutboxMessageAsync(entity.Id);
        message.Should().NotBeNull();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChanges_ThroughBaseDbContextReference_ShouldWriteOutboxMessages()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        entity.RaiseCreatedEvent();
        Db.TestEntities.Add(entity);
        DbContext dbContext = Db;

        // Act
        await dbContext.SaveChangesAsync();

        // Assert
        var message = await GetOutboxMessageAsync(entity.Id);
        message.Should().NotBeNull();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChanges_WhenSaveFailsAndIsRetried_ShouldAddOutboxMessagesOnlyOnce()
    {
        // Arrange
        var existing = new TestEntity { Name = "Existing" };
        await using (var seedContext = Fixture.CreateDbContext())
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

        entity.DomainEvents.Should().NotBeEmpty();

        entity.Id = Guid.NewGuid();
        await Db.SaveChangesAsync();

        // Assert
        await using var context = Fixture.CreateDbContext();
        var messages = await context.OutboxMessages
            .Where(x => x.Content.Contains(existing.Id.ToString()))
            .ToListAsync();

        messages.Should().ContainSingle();
        entity.DomainEvents.Should().BeEmpty();
    }

    private async Task<Outbox.OutboxMessage?> GetOutboxMessageAsync(Guid aggregateId)
    {
        await using var context = Fixture.CreateDbContext();
        return await context.OutboxMessages
            .SingleOrDefaultAsync(x => x.Content.Contains(aggregateId.ToString()));
    }
}
