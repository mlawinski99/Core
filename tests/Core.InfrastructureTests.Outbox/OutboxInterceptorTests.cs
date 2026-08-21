using Core.Infrastructure.Json;
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

    [Fact]
    public async Task SavingChanges_WhenSerializationFailsPartway_ShouldNotDuplicateEarlierEventsOnRetry()
    {
        // Arrange
        var serializer = new FailingJsonSerializer { FailOnCall = 2 };
        await using var db = PostgresFixture.CreateDbContext(new OutboxInterceptor(serializer));

        var entity = new TestEntity { Name = "Test" };
        entity.RaiseCreatedEvent();
        entity.RaiseCreatedEvent();
        db.TestEntities.Add(entity);

        // Act
        var failedSave = () => db.SaveChangesAsync();
        await failedSave.Should().ThrowAsync<InvalidOperationException>();

        serializer.FailOnCall = 0;
        await db.SaveChangesAsync();

        // Assert
        var messages = await db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.AggregateId == entity.Id)
            .ToListAsync();

        messages.Should().HaveCount(2);
    }

    private class FailingJsonSerializer : IJsonSerializer
    {
        private readonly TestJsonSerializer _inner = new();
        private int _calls;

        public int FailOnCall { get; set; }

        public T Deserialize<T>(string value) => _inner.Deserialize<T>(value);

        public string Serialize<T>(T value)
        {
            _calls++;

            if (_calls == FailOnCall)
                throw new InvalidOperationException();

            return _inner.Serialize(value);
        }
    }
}
