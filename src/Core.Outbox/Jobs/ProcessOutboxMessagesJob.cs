using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using Core.DateTimeProvider;
using Core.KafkaProducer;
using Core.Logger;
using Core.Outbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Outbox.Jobs;

[DisallowConcurrentExecution]
// no retries, the next scheduled run picks up whatever failed
[Retry(0)]
public class ProcessOutboxMessagesJob<TContext>(
    TContext db,
    IAppLogger<ProcessOutboxMessagesJob<TContext>> logger,
    IProducer<OutboxMessage> producer,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> options)
    : IBackgroundJob
    where TContext : DbContext, IOutbox
{
    public const string JobId = "process-outbox-messages";

    private readonly OutboxOptions _options = options.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        var messages = await LoadPublishableAsync(cancellationToken);
        var outdatedIds = await LoadOutdatedIdsAsync(messages, cancellationToken);

        foreach (var message in messages.Where(m => outdatedIds.Contains(m.Id)))
        {
            MarkOutdated(message);
        }

        var outcomes = await PublishAsync([.. messages.Where(m => !outdatedIds.Contains(m.Id))], cancellationToken);

        foreach (var outcome in outcomes)
        {
            Apply(outcome);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<List<OutboxMessage>> LoadPublishableAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;

        return db.OutboxMessages
            .Where(m =>
                // not published yet
                m.ProcessedOn == null

                // due: never failed, or its retry time has come
                && (m.NextRetryUtc == null || m.NextRetryUtc <= now)

                // no earlier blocking message of the same aggregate is waiting for its retry
                && !db.OutboxMessages.Any(earlier =>
                    earlier.AggregateId == m.AggregateId
                    && earlier.OccurredOnUtc < m.OccurredOnUtc
                    && earlier.IsBlocking
                    && earlier.ProcessedOn == null
                    && earlier.NextRetryUtc > now)

                // no earlier blocking message of the same aggregate is dead-lettered
                && !db.DeadLetterOutboxMessages.Any(dead =>
                    dead.AggregateId == m.AggregateId
                    && dead.OccurredOnUtc < m.OccurredOnUtc
                    && dead.IsBlocking))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<Guid>> LoadOutdatedIdsAsync(List<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        var nonBlockingIds = messages
            .Where(m => !m.IsBlocking)
            .Select(m => m.Id)
            .ToList();

        return db.OutboxMessages
            .Where(m =>
                // non-blocking messages of this batch
                nonBlockingIds.Contains(m.Id)

                // a newer one of the same type and aggregate is published
                && (db.OutboxMessages.Any(newer =>
                        newer.AggregateId == m.AggregateId
                        && newer.Type == m.Type
                        && newer.OccurredOnUtc > m.OccurredOnUtc
                        && newer.Outcome == OutboxMessageOutcome.Published)

                    // or already archived, e.g. when a dead letter is replayed days later
                    || db.ProcessedOutboxMessages.Any(newer =>
                        newer.AggregateId == m.AggregateId
                        && newer.Type == m.Type
                        && newer.OccurredOnUtc > m.OccurredOnUtc
                        && newer.Outcome == OutboxMessageOutcome.Published)))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<PublishOutcome>> PublishAsync(List<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        var outcomesPerAggregate = await Task.WhenAll(messages
            .GroupBy(m => m.AggregateId)
            .Select(aggregate => PublishAggregateAsync([.. aggregate], cancellationToken)));

        return [.. outcomesPerAggregate.SelectMany(o => o)];
    }

    private async Task<List<PublishOutcome>> PublishAggregateAsync(List<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        var outcomes = new List<PublishOutcome>();

        foreach (var message in messages)
        {
            var result = await producer.ProduceAsync(message.Type, message, message.AggregateId.ToString(), cancellationToken);
            outcomes.Add(new PublishOutcome(message, result));

            if (StopsAggregate(message, result))
            {
                break;
            }
        }

        return outcomes;
    }

    // unavailable: the rest would fail too; rejected blocking: the rest must wait for it
    private static bool StopsAggregate(OutboxMessage message, ProduceResult result) =>
        result.Status == ProduceStatus.Unavailable
        || (result.Status == ProduceStatus.Rejected && message.IsBlocking);

    private void MarkOutdated(OutboxMessage message)
    {
        message.ProcessedOn = dateTimeProvider.UtcNow;
        message.Outcome = OutboxMessageOutcome.Outdated;

        logger.LogInformation("Outbox message {MessageId} of type {Type} for aggregate {AggregateId} outdated by a newer published message",
            message.Id, message.Type, message.AggregateId);
    }

    private void Apply(PublishOutcome outcome)
    {
        var (message, result) = outcome;

        switch (result.Status)
        {
            case ProduceStatus.Delivered:
                message.ProcessedOn = dateTimeProvider.UtcNow;
                message.Outcome = OutboxMessageOutcome.Published;
                logger.LogInformation("Published outbox message {MessageId} of type {Type}, correlation {CorrelationId}",
                    message.Id, message.Type, message.CorrelationId ?? "NULL");
                break;
            // the broker is down, not the message's fault, so it does not count as a retry
            case ProduceStatus.Unavailable:
                message.LastError = result.Error;
                logger.LogWarning("Kafka unavailable for outbox message {MessageId}; skipping aggregate {AggregateId} for the rest of this run: {Error}",
                    message.Id, message.AggregateId, result.Error);
                break;
            case ProduceStatus.Rejected:
                MarkRejected(message, result.Error);
                break;
        }
    }

    private void MarkRejected(OutboxMessage message, string? error)
    {
        message.RetryCount++;
        message.LastError = error;

        if (message.RetryCount >= _options.MaxRetries)
        {
            MoveToDeadLetters(message);
            return;
        }

        message.NextRetryUtc = dateTimeProvider.UtcNow.AddMinutes(Math.Pow(_options.RetryBackoffBase, message.RetryCount));

        logger.LogWarning("Retry {RetryCount} of {MaxRetries} scheduled for outbox message {MessageId} at {NextRetryUtc}: {Error}",
            message.RetryCount, _options.MaxRetries, message.Id, message.NextRetryUtc, error);
    }

    private void MoveToDeadLetters(OutboxMessage message)
    {
        db.DeadLetterOutboxMessages.Add(OutboxMapper.ToDeadLetter(message, dateTimeProvider.UtcNow));
        db.OutboxMessages.Remove(message);

        logger.LogError("Outbox message {MessageId} of type {Type} for aggregate {AggregateId} dead-lettered after {RetryCount} attempts, correlation {CorrelationId}: {LastError}",
            message.Id, message.Type, message.AggregateId, message.RetryCount, message.CorrelationId ?? "NULL", message.LastError);
    }

    private record PublishOutcome(OutboxMessage Message, ProduceResult Result);
}
