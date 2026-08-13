using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Logger;
using Core.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("Outbox")]
public class MoveProcessedOutboxMessagesJobTests(PostgresFixture postgresFixture) : IntegrationTestBase(postgresFixture)
{
    private readonly IAppLogger<MoveProcessedOutboxMessagesJob<TestDbContext>> _logger =
        Substitute.For<IAppLogger<MoveProcessedOutboxMessagesJob<TestDbContext>>>();
    private readonly string _testId = Guid.NewGuid().ToString();
    private readonly OutboxOptions _options = new() { MoveAfterDays = 7, MoveBatchSize = 2 };

    private MoveProcessedOutboxMessagesJob<TestDbContext> _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _job = new MoveProcessedOutboxMessagesJob<TestDbContext>(Db, _logger, DateTimeProvider, Options.Create(_options));

        await Db.OutboxMessages.ExecuteDeleteAsync();
        await Db.ProcessedOutboxMessages.ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
        {
            await Db.OutboxMessages.ExecuteDeleteAsync();
            await Db.ProcessedOutboxMessages.ExecuteDeleteAsync();
        }

        await base.DisposeAsync();
    }

    [Fact]
    public async Task Run_WithMessageProcessedOutsideMoveWindow_ShouldMoveItToProcessedMessages()
    {
        // Arrange
        var message = CreateMessage();
        message.CorrelationId = "trace-id";
        message.ProcessedOn = DateTimeProvider.UtcNow.AddDays(-8);
        message.IsProcessed = true;
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        remaining.Should().BeFalse();

        var moved = await Db.ProcessedOutboxMessages
            .AsNoTracking()
            .FirstAsync(m => m.Id == message.Id);
        moved.AggregateId.Should().Be(message.AggregateId);
        moved.Type.Should().Be(message.Type);
        moved.Content.Should().Be(message.Content);
        moved.CorrelationId.Should().Be("trace-id");
        moved.ProcessedOn.Should().Be(message.ProcessedOn);
        moved.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithMessageProcessedInsideMoveWindow_ShouldNotMove()
    {
        // Arrange
        var message = CreateMessage();
        message.ProcessedOn = DateTimeProvider.UtcNow.AddDays(-6);
        message.IsProcessed = true;
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        remaining.Should().BeTrue();

        var moved = await Db.ProcessedOutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        moved.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithUnprocessedMessage_ShouldNotMove()
    {
        // Arrange
        var message = CreateMessage();
        message.OccurredOnUtc = DateTimeProvider.UtcNow.AddDays(-30);
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        remaining.Should().BeTrue();

        var moved = await Db.ProcessedOutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        moved.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithBlockingMessage_ShouldNotMove()
    {
        // Arrange
        var message = CreateMessage();
        message.OccurredOnUtc = DateTimeProvider.UtcNow.AddDays(-30);
        message.RetryCount = 5;
        message.StoppedRetryingUtc = DateTimeProvider.UtcNow.AddDays(-29);
        message.LastError = "test";
        Db.OutboxMessages.Add(message);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        remaining.Should().BeTrue();

        var moved = await Db.ProcessedOutboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == message.Id);
        moved.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithMoreMessagesThanBatchSize_ShouldMoveAllOfThem()
    {
        // Arrange
        var messages = Enumerable.Range(0, 5).Select(_ =>
        {
            var message = CreateMessage();
            message.ProcessedOn = DateTimeProvider.UtcNow.AddDays(-8);
            message.IsProcessed = true;
            return message;
        }).ToList();

        Db.OutboxMessages.AddRange(messages);
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var ids = messages
            .Select(m => m.Id)
            .ToList();

        var remaining = await Db.OutboxMessages
            .AsNoTracking()
            .CountAsync(m => ids.Contains(m.Id));
        remaining.Should().Be(0);

        var moved = await Db.ProcessedOutboxMessages
            .AsNoTracking()
            .CountAsync(m => ids.Contains(m.Id));
        moved.Should().Be(5);
    }

    private OutboxMessage CreateMessage() => new()
    {
        Id = Guid.NewGuid(),
        AggregateId = Guid.NewGuid(),
        OccurredOnUtc = DateTimeProvider.UtcNow.AddDays(-10),
        Type = $"test-topic-{_testId}",
        Content = "{\"key\":\"value\"}",
        ProcessedOn = null,
        IsProcessed = false
    };
}
