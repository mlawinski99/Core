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
        await Db.DeadLetterOutboxMessages.ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
        {
            await Db.OutboxMessages.ExecuteDeleteAsync();
            await Db.DeadLetterOutboxMessages.ExecuteDeleteAsync();
        }

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
            .Returns(new ProduceResult(ProduceStatus.Delivered));

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.Outcome.Should().Be(OutboxMessageOutcome.Published);
        updated.ProcessedOn.Should().Be(DateTimeProvider.UtcNow);

        _logger.Received().LogInformation(
            Arg.Is<string>(s => s.Contains("Published outbox message")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id)) && args.Any(a => a.Equals("trace-id"))));
    }

    [Fact]
    public async Task Run_WithProduceFailure_ShouldLogRetryWarningAndNotMarkAsProcessed()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Rejected, "rejected"));

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.Outcome.Should().BeNull();
        updated.ProcessedOn.Should().BeNull();

        _logger.Received().LogWarning(
            Arg.Is<string>(s => s.Contains("Retry")),
            Arg.Is<object[]>(args => args.Any(a => a.Equals(message.Id))));
    }

    [Fact]
    public async Task Run_ShouldSkipAlreadyProcessedMessages()
    {
        // Arrange
        var processed = CreateUnprocessedMessage();
        processed.ProcessedOn = DateTimeProvider.UtcNow;
        processed.Outcome = OutboxMessageOutcome.Published;

        Db.OutboxMessages.Add(processed);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync(processed.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithMultipleMessagesForSameAggregate_ShouldProcessInChronologicalOrder()
    {
        // Arrange
        var older = CreateUnprocessedMessage();
        older.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        older.Type = "older-topic";

        var newer = CreateUnprocessedMessage();
        newer.AggregateId = older.AggregateId;
        newer.OccurredOnUtc = DateTimeProvider.UtcNow;
        newer.Type = "newer-topic";

        Db.OutboxMessages.AddRange(newer, older);
        await Db.SaveChangesAsync();

        var producedTopics = new List<string>();
        _producer.ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered))
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
            .Returns(new ProduceResult(ProduceStatus.Delivered));

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.Received()
            .ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), message.AggregateId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithFailedBlockingMessage_ShouldSkipSameAggregateAndPublishOthers()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var failing = CreateUnprocessedMessage();
        failing.AggregateId = aggregateId;
        failing.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        failing.Type = "failing-topic";
        failing.IsBlocking = true;

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
            .Returns(new ProduceResult(ProduceStatus.Rejected, "rejected"));
        _producer.ProduceAsync("same-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered));
        _producer.ProduceAsync("other-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered));

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync("same-aggregate-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var skipped = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == sameAggregate.Id);
        skipped.Outcome.Should().BeNull();
        skipped.RetryCount.Should().Be(0);

        var published = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == otherAggregate.Id);
        published.Outcome.Should().Be(OutboxMessageOutcome.Published);
    }

    [Fact]
    public async Task Run_WithProduceFailure_ShouldIncrementRetryCountAndScheduleNextRetry()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Rejected, "rejected"));

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var updated = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        updated.RetryCount.Should().Be(1);
        updated.NextRetryUtc.Should().Be(DateTimeProvider.UtcNow.AddMinutes(2));
        updated.LastError.Should().Be("rejected");
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
    public async Task Run_WithBlockingFailureOnFinalAttempt_ShouldMoveToDeadLettersAndBlockLaterMessages()
    {
        // Arrange
        var message = CreateUnprocessedMessage();
        message.RetryCount = 4;
        message.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        message.IsBlocking = true;

        var later = CreateUnprocessedMessage();
        later.AggregateId = message.AggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.OutboxMessages.AddRange(message, later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Rejected, "rejected"));
        _producer.ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered));

        // Act
        await _job.Run(CancellationToken.None);
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.OutboxMessages.AsNoTracking().AnyAsync(m => m.Id == message.Id);
        remaining.Should().BeFalse();

        var deadLetter = await Db.DeadLetterOutboxMessages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        deadLetter.RetryCount.Should().Be(5);
        deadLetter.LastError.Should().Be("rejected");
        deadLetter.OccurredOnUtc.Should().Be(message.OccurredOnUtc);
        deadLetter.DeadLetteredOnUtc.Should().Be(DateTimeProvider.UtcNow);

        await _producer.Received(1)
            .ProduceAsync(message.Type, Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        await _producer.DidNotReceive()
            .ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var blocked = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == later.Id);
        blocked.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithBlockingMessageAwaitingRetry_ShouldNotPublishLaterMessageForSameAggregate()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var failing = CreateUnprocessedMessage();
        failing.AggregateId = aggregateId;
        failing.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10);
        failing.Type = "failing-topic";
        failing.IsBlocking = true;

        var later = CreateUnprocessedMessage();
        later.AggregateId = aggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.OutboxMessages.AddRange(failing, later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync("failing-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Rejected, "rejected"));
        _producer.ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered));

        // Act
        await _job.Run(CancellationToken.None);
        await _job.Run(CancellationToken.None);

        // Assert
        await _producer.DidNotReceive()
            .ProduceAsync("later-topic", Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var blocked = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == later.Id);
        blocked.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithBlockingDeadLetter_ShouldNotPublishLaterMessageForSameAggregate()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        var deadLetter = new DeadLetterOutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-10),
            Type = "dead-letter-topic",
            Content = "{\"key\":\"value\"}",
            IsBlocking = true,
            RetryCount = 5,
            DeadLetteredOnUtc = DateTimeProvider.UtcNow
        };

        var later = CreateUnprocessedMessage();
        later.AggregateId = aggregateId;
        later.OccurredOnUtc = DateTimeProvider.UtcNow.AddMinutes(-5);
        later.Type = "later-topic";

        Db.DeadLetterOutboxMessages.Add(deadLetter);
        Db.OutboxMessages.Add(later);
        await Db.SaveChangesAsync();

        _producer.ProduceAsync(Arg.Any<string>(), Arg.Any<OutboxMessage>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ProduceResult(ProduceStatus.Delivered));

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
            .Returns<ProduceResult>(_ =>
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
        ProcessedOn = null
    };
}
