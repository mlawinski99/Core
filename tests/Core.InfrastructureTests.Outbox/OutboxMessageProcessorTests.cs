using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.KafkaProducer;
using Core.Logger;
using Core.Outbox;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("Outbox")]
public class OutboxMessageProcessorTests(PostgresFixture postgresFixture) : IntegrationTestBase(postgresFixture)
{
    private readonly IProducer<OutboxMessage> _producer = Substitute.For<IProducer<OutboxMessage>>();
    private readonly IAppLogger<OutboxMessageProcessor<TestDbContext>> _logger =
        Substitute.For<IAppLogger<OutboxMessageProcessor<TestDbContext>>>();
    private readonly string _testId = Guid.NewGuid().ToString();

    private OutboxMessageProcessor<TestDbContext> _processor = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _processor = new OutboxMessageProcessor<TestDbContext>(Db, _logger, _producer, DateTimeProvider);

        await Db.OutboxMessages.ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
            await Db.OutboxMessages.ExecuteDeleteAsync();

        await base.DisposeAsync();
    }

    [Fact]
    public async Task ProcessAsync_WithUnprocessedMessage_ShouldProduceAndMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.IsProcessed.Should().BeTrue();
        updated.ProcessedOn.Should().Be(DateTimeProvider.UtcNow);
    }

    [Fact]
    public async Task ProcessAsync_WithProduceFailure_ShouldLogErrorAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.IsProcessed.Should().BeFalse();
        updated.ProcessedOn.Should().BeNull();

        _logger.Received().LogError(
            Arg.Is<string>(s => s.Contains("{MessageId}")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id))));
    }

    [Fact]
    public async Task ProcessAsync_WithProduceException_ShouldLogErrorAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Kafka unavailable"));

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.IsProcessed.Should().BeFalse();
        updated.ProcessedOn.Should().BeNull();

        _logger.Received().LogError(
            Arg.Any<Exception>(),
            Arg.Is<string>(s => s.Contains("{MessageId}")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id))));
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipAlreadyProcessedMessages()
    {
        // Arrange
        var processed = CreateUnprocessedMessage();
        processed.ProcessedOn = DateTimeProvider.UtcNow;
        processed.IsProcessed = true;

        Db.OutboxMessages.Add(processed);
        await Db.SaveChangesAsync();

        // Act
        await _processor.ProcessAsync();

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(processed.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleMessages_ShouldProcessInChronologicalOrder()
    {
        // Arrange
        var older = CreateUnprocessedMessage();
        older.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        older.Type = "older-topic";

        var newer = CreateUnprocessedMessage();
        newer.OccurredOnUtc = DateTimeProvider.UtcNow;
        newer.Type = "newer-topic";

        Db.OutboxMessages.AddRange(newer, older);
        await Db.SaveChangesAsync();

        var producedTopics = new List<string>();
        _producer.ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(ci => producedTopics.Add(ci.ArgAt<string>(0)));

        // Act
        await _processor.ProcessAsync();

        // Assert
        producedTopics.Should().ContainInOrder("older-topic", "newer-topic");
    }

    [Fact]
    public async Task ProcessAsync_WithNoUnprocessedMessages_ShouldNotProduce()
    {
        // Act
        await _processor.ProcessAsync();

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private OutboxMessage CreateUnprocessedMessage() => new()
    {
        Id = Guid.NewGuid(),
        OccurredOnUtc = DateTimeProvider.UtcNow,
        Type = $"test-topic-{_testId}",
        Content = "{\"key\":\"value\"}",
        ProcessedOn = null,
        IsProcessed = false
    };
}
