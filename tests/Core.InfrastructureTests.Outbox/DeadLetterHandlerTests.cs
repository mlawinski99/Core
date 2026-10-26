using Core.CQRS;
using Core.InfrastructureTests.Outbox.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Outbox.Commands;
using Core.Outbox.Entities;
using Core.Outbox.Queries;
using Core.ResultPattern;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.InfrastructureTests.Outbox;

[Collection("OutboxApi")]
public class DeadLetterHandlerTests(OutboxApiFixture fixture) : IAsyncLifetime
{
    private readonly IServiceScope _scope = fixture.Services.CreateScope();

    private IRequestDispatcher Dispatcher => _scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

    private TestDbContext Db { get; } = fixture.CreateDbContext();

    public async Task InitializeAsync()
    {
        await Db.OutboxMessages.ExecuteDeleteAsync();
        await Db.DeadLetterOutboxMessages.ExecuteDeleteAsync();
        await Db.ProcessedOutboxMessages.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.OutboxMessages.ExecuteDeleteAsync();
        await Db.DeadLetterOutboxMessages.ExecuteDeleteAsync();
        await Db.ProcessedOutboxMessages.ExecuteDeleteAsync();

        await Db.DisposeAsync();
        _scope.Dispose();
    }

    [Fact]
    public async Task ReplayDeadLetter_WithDeadLetter_ShouldMoveBackWithOriginalOccurredOnAndResetRetries()
    {
        // Arrange
        var deadLetter = new DeadLetterOutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            OccurredOnUtc = fixture.DateTimeProvider.UtcNow.AddMinutes(-10),
            Type = "dead-letter-topic",
            Content = "{\"key\":\"value\"}",
            IsBlocking = true,
            RetryCount = 5,
            LastError = "rejected",
            DeadLetteredOnUtc = fixture.DateTimeProvider.UtcNow
        };
        Db.DeadLetterOutboxMessages.Add(deadLetter);
        await Db.SaveChangesAsync();

        // Act
        var result = await Dispatcher.Dispatch(new ReplayDeadLetterCommand(deadLetter.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();

        var replayed = await Db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == deadLetter.Id);
        replayed.OccurredOnUtc.Should().Be(deadLetter.OccurredOnUtc);
        replayed.IsBlocking.Should().BeTrue();
        replayed.RetryCount.Should().Be(0);
        replayed.NextRetryUtc.Should().BeNull();
        replayed.LastError.Should().Be("rejected");
        replayed.Outcome.Should().BeNull();

        var remaining = await Db.DeadLetterOutboxMessages.AsNoTracking().AnyAsync(d => d.Id == deadLetter.Id);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task ReplayDeadLetter_WithUnknownId_ShouldReturnNotFound()
    {
        // Act
        var result = await Dispatcher.Dispatch(new ReplayDeadLetterCommand(Guid.NewGuid()));

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }

    [Fact]
    public async Task DiscardDeadLetter_WithDeadLetter_ShouldArchiveAsDiscarded()
    {
        // Arrange
        var deadLetter = new DeadLetterOutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            OccurredOnUtc = fixture.DateTimeProvider.UtcNow.AddMinutes(-10),
            Type = "dead-letter-topic",
            Content = "{\"key\":\"value\"}",
            RetryCount = 5,
            LastError = "rejected",
            DeadLetteredOnUtc = fixture.DateTimeProvider.UtcNow
        };
        Db.DeadLetterOutboxMessages.Add(deadLetter);
        await Db.SaveChangesAsync();

        // Act
        var result = await Dispatcher.Dispatch(new DiscardDeadLetterCommand(deadLetter.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();

        var discarded = await Db.ProcessedOutboxMessages.AsNoTracking().FirstAsync(m => m.Id == deadLetter.Id);
        discarded.Outcome.Should().Be(OutboxMessageOutcome.Discarded);
        discarded.ProcessedOn.Should().Be(fixture.DateTimeProvider.UtcNow);
        discarded.LastError.Should().Be("rejected");

        var remaining = await Db.DeadLetterOutboxMessages.AsNoTracking().AnyAsync(d => d.Id == deadLetter.Id);
        remaining.Should().BeFalse();
    }

    [Fact]
    public async Task DiscardDeadLetter_WithUnknownId_ShouldReturnNotFound()
    {
        // Act
        var result = await Dispatcher.Dispatch(new DiscardDeadLetterCommand(Guid.NewGuid()));

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }

    [Fact]
    public async Task GetDeadLetters_ShouldReturnNewestFirstPaged()
    {
        // Arrange
        var deadLetters = Enumerable.Range(1, 3).Select(i => new DeadLetterOutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            OccurredOnUtc = fixture.DateTimeProvider.UtcNow.AddHours(-10),
            Type = "dead-letter-topic",
            Content = "{\"key\":\"value\"}",
            RetryCount = 5,
            DeadLetteredOnUtc = fixture.DateTimeProvider.UtcNow.AddHours(-i)
        }).ToList();
        Db.DeadLetterOutboxMessages.AddRange(deadLetters);
        await Db.SaveChangesAsync();

        // Act
        var result = await Dispatcher.Dispatch(new GetDeadLettersQuery(1, 2));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(3);
        result.Data.Items.Select(d => d.Id).Should().Equal(deadLetters[0].Id, deadLetters[1].Id);
    }
}