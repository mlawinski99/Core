using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.KafkaProducer;
using Core.Logger;
using Core.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("Outbox")]
public class ProcessOutboxMessagesJobTests(PostgresFixture postgresFixture) : IntegrationTestBase(postgresFixture)
{
    private readonly IProducer<OutboxMessage> _producer = Substitute.For<IProducer<OutboxMessage>>();
    private readonly IAppLogger<ProcessOutboxMessagesJob<TestDbContext>> _logger =
        Substitute.For<IAppLogger<ProcessOutboxMessagesJob<TestDbContext>>>();
    private readonly string _testId = Guid.NewGuid().ToString();

    private ProcessOutboxMessagesJob<TestDbContext> _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _job = new ProcessOutboxMessagesJob<TestDbContext>(Db, _logger, _producer, DateTimeProvider, Options.Create(new OutboxOptions()));

        await Db.OutboxMessages.ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
            await Db.OutboxMessages.ExecuteDeleteAsync();

        await base.DisposeAsync();
    }

    [Fact]
    public async Task Run_WithUnprocessedMessage_ShouldProduceAndMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        message.CorrelationId = "trace-id";
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.IsProcessed.Should().BeTrue();
        updated.ProcessedOn.Should().Be(DateTimeProvider.UtcNow);

        _logger.Received().LogInformation(
            Arg.Is<string>(s => s.Contains("Published outbox message")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id)) && args.Any(a => a.Equals("trace-id"))));
    }

    [Fact]
    public async Task Run_WithProduceFailure_ShouldLogErrorAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.IsProcessed.Should().BeFalse();
        updated.ProcessedOn.Should().BeNull();

        _logger.Received().LogError(
            Arg.Is<string>(s => s.Contains("{MessageId}")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id))));
    }

    [Fact]
    public async Task Run_WithProduceException_ShouldLogErrorAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Kafka unavailable"));

        // Act
        await _job.Run(CancellationToken.None);

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
    public async Task Run_ShouldSkipAlreadyProcessedMessages()
    {
        // Arrange
        var processed = CreateUnprocessedMessage();
        processed.ProcessedOn = DateTimeProvider.UtcNow;
        processed.IsProcessed = true;

        Db.OutboxMessages.Add(processed);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(processed.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithMultipleMessages_ShouldProcessInChronologicalOrder()
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
        await _job.Run(CancellationToken.None);

        // Assert
        producedTopics.Should().ContainInOrder("older-topic", "newer-topic");
    }

    [Fact]
    public async Task Run_ShouldKeyMessagesByAggregateId()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        message.CorrelationId = "correlation-id";

        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.Received()
            .ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), message.AggregateId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithFailedMessage_ShouldSkipSameAggregateAndPublishOthers()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var failing = CreateUnprocessedMessage();
        failing.AggregateId = aggregateId;
        failing.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        failing.Type = "failing-topic";

        var sameAggregate = CreateUnprocessedMessage();
        sameAggregate.AggregateId = aggregateId;
        sameAggregate.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        sameAggregate.Type = "same-aggregate-topic";

        var otherAggregate = CreateUnprocessedMessage();
        otherAggregate.OccurredOnUtc = DateTimeProvider.UtcNow;
        otherAggregate.Type = "other-aggregate-topic";

        Db.OutboxMessages.AddRange(failing, sameAggregate, otherAggregate);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync("failing-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _producer.ProduceAsync("same-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _producer.ProduceAsync("other-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync("same-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var skipped = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == sameAggregate.Id);
        skipped.IsProcessed.Should().BeFalse();
        skipped.RetryCount.Should().Be(0);

        var published = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == otherAggregate.Id);
        published.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithProduceFailure_ShouldIncrementRetryCountAndScheduleNextRetry()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.RetryCount.Should().Be(1);
        updated.NextRetryUtc.Should().Be(DateTimeProvider.UtcNow.AddMinutes(2));
        updated.LastError.Should().NotBeNullOrEmpty();
        updated.StoppedRetryingUtc.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithMessageScheduledInFuture_ShouldNotProduceYet()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        message.RetryCount = 1;
        message.NextRetryUtc = DateTimeProvider.UtcNow.AddMinutes(5);

        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithFailureOnFinalAttempt_ShouldStopRetryingAndExcludeFromLaterRuns()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        message.RetryCount = 4;
        message.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);

        var later = CreateUnprocessedMessage();
        later.AggregateId = message.AggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.OutboxMessages.AddRange(message, later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _producer.ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.RetryCount.Should().Be(5);
        updated.StoppedRetryingUtc.Should().Be(DateTimeProvider.UtcNow);
        updated.LastError.Should().NotBeNullOrEmpty();

        await _producer.Received(1)
            .ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        await _producer.DidNotReceive()
            .ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var blocked = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == later.Id);
        blocked.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithFailedMessageAwaitingRetry_ShouldNotPublishLaterMessageForSameAggregate()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var failing = CreateUnprocessedMessage();
        failing.AggregateId = aggregateId;
        failing.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        failing.Type = "failing-topic";

        var later = CreateUnprocessedMessage();
        later.AggregateId = aggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.OutboxMessages.AddRange(failing, later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync("failing-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _producer.ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var blocked = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == later.Id);
        blocked.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithParkedMessage_ShouldNotPublishLaterMessageForSameAggregate()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var parked = CreateUnprocessedMessage();
        parked.AggregateId = aggregateId;
        parked.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        parked.RetryCount = 5;
        parked.StoppedRetryingUtc = DateTimeProvider.UtcNow;
        parked.Type = "parked-topic";

        var later = CreateUnprocessedMessage();
        later.AggregateId = aggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.OutboxMessages.AddRange(parked, later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WhenCancelled_ShouldNotCountAsDeliveryFailure()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        using var cts = new CancellationTokenSource();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act
        var act = async () => await _job.Run(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.RetryCount.Should().Be(0);
        updated.LastError.Should().BeNull();
        updated.NextRetryUtc.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithNoUnprocessedMessages_ShouldNotProduce()
    {
        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private OutboxMessage CreateUnprocessedMessage() => new()
    {
        Id = Guid.NewGuid(),
        AggregateId = Guid.NewGuid(),
        OccurredOnUtc = DateTimeProvider.UtcNow,
        Type = $"test-topic-{_testId}",
        Content = "{\"key\":\"value\"}",
        ProcessedOn = null,
        IsProcessed = false
    };
}
