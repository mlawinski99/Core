using Core.InfrastructureTests.Outbox.Fixtures;
using Core.KafkaProducer;
using Core.Logger;
using Core.Outbox;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("Outbox")]
public class OutboxMessageProcessorTests : IAsyncLifetime
{
    private readonly OutboxTestFixture _fixture;
    private readonly TestOutboxDbContext _db;
    private readonly IProducer<OutboxMessage> _producer;
    private readonly IAppLogger<OutboxMessageProcessor<TestOutboxDbContext>> _logger;
    private readonly OutboxMessageProcessor<TestOutboxDbContext> _processor;
    private readonly string _testId = Guid.NewGuid().ToString();

    public OutboxMessageProcessorTests(OutboxTestFixture fixture)
    {
        _fixture = fixture;
        _db = fixture.CreateOutboxDbContext();
        _producer = Substitute.For<IProducer<OutboxMessage>>();
        _logger = Substitute.For<IAppLogger<OutboxMessageProcessor<TestOutboxDbContext>>>();

        _processor = new OutboxMessageProcessor<TestOutboxDbContext>(
            _db, _logger, _producer, _fixture.DateTimeProvider);
    }

    public async Task InitializeAsync()
    {
        _db.OutboxMessages.RemoveRange(_db.OutboxMessages);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _db.OutboxMessages.RemoveRange(_db.OutboxMessages);
        await _db.SaveChangesAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ProcessAsync_WithUnprocessedMessage_ShouldProduceAndMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await _db.OutboxMessages.FindAsync(message.Id);
        updated!.IsProcessed.Should().BeTrue();
        updated.ProcessedOn.Should().Be(_fixture.DateTimeProvider.UtcNow);
    }

    [Fact]
    public async Task ProcessAsync_WithProduceFailure_ShouldLogErrorAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await _db.OutboxMessages.FindAsync(message.Id);
        updated!.IsProcessed.Should().BeFalse();
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
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Kafka unavailable"));

        // Act
        await _processor.ProcessAsync();

        // Assert
        var updated = await _db.OutboxMessages.FindAsync(message.Id);
        updated!.IsProcessed.Should().BeFalse();
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
        processed.ProcessedOn = _fixture.DateTimeProvider.UtcNow;
        processed.IsProcessed = true;

        _db.OutboxMessages.Add(processed);
        await _db.SaveChangesAsync();

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
        older.OccurredOnUtc = _fixture.DateTimeProvider.UtcNow.AddMinutes(-10);
        older.Type = "older-topic";

        var newer = CreateUnprocessedMessage();
        newer.OccurredOnUtc = _fixture.DateTimeProvider.UtcNow;
        newer.Type = "newer-topic";

        _db.OutboxMessages.AddRange(newer, older);
        await _db.SaveChangesAsync();

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
            .ProduceAsync(Arg.Is<string>(t => t.Contains(_testId)), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private OutboxMessage CreateUnprocessedMessage() => new()
    {
        Id = Guid.NewGuid(),
        OccurredOnUtc = _fixture.DateTimeProvider.UtcNow,
        Type = $"test-topic-{_testId}",
        Content = "{\"key\":\"value\"}",
        ProcessedOn = null,
        IsProcessed = false
    };
}
