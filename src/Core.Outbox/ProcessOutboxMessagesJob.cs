using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using Core.DateTimeProvider;
using Core.KafkaProducer;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Outbox;

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
        var now = dateTimeProvider.UtcNow;

        // we want to block aggregate if any message fails @TODO flag or type on domainEvent for blocking aggregate or not
        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var outcomes = await Task.WhenAll(messages
            .GroupBy(m => m.AggregateId)
            .Select(g => PublishAggregateAsync([.. g], now, cancellationToken)));

        foreach (var outcome in outcomes.SelectMany(o => o))
        {
            if (outcome.Error is null)
            {
                outcome.Message.ProcessedOn = dateTimeProvider.UtcNow;
                outcome.Message.IsProcessed = true;

                logger.LogInformation("Published outbox message {MessageId} of type {Type}, correlation {CorrelationId}",
                    outcome.Message.Id, outcome.Message.Type, outcome.Message.CorrelationId ?? "NULL");
            }
            else
            {
                MarkFailed(outcome.Message, outcome.Error);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<PublishOutcome>> PublishAggregateAsync(IReadOnlyList<OutboxMessage> messages, DateTime now, CancellationToken cancellationToken)
    {
        var outcomes = new List<PublishOutcome>();

        foreach (var message in messages)
        {
            if (message.StoppedRetryingUtc is not null || message.NextRetryUtc > now)
            {
                logger.LogWarning("Outbox message {MessageId} for aggregate {AggregateId} is awaiting retry or blocked; stopping the aggregate for this run", message.Id, message.AggregateId);
                break;
            }

            try
            {
                var isProduceSucceeded = await producer.ProduceAsync(message.Type, message, message.AggregateId.ToString(), cancellationToken);

                if (isProduceSucceeded)
                {
                    outcomes.Add(new PublishOutcome(message, null));
                }
                else
                {
                    logger.LogError("Failed to produce outbox message {MessageId} to topic {Topic}", message.Id, message.Type);
                    outcomes.Add(new PublishOutcome(message, $"Producer returned false for topic {message.Type}"));
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                outcomes.Add(new PublishOutcome(message, ex.Message));
                break;
            }
        }

        return outcomes;
    }

    private void MarkFailed(OutboxMessage message, string error)
    {
        message.RetryCount++;
        message.LastError = error;

        if (message.RetryCount >= _options.MaxRetries)
        {
            message.StoppedRetryingUtc = dateTimeProvider.UtcNow;

            logger.LogError("Outbox message {MessageId} of type {Type} for aggregate {AggregateId} parked after {RetryCount} attempts, correlation {CorrelationId}: {LastError}. Aggregate stays blocked until the message is replayed",
                message.Id, message.Type, message.AggregateId, message.RetryCount, message.CorrelationId ?? "NULL", error);
        }
        else
        {
            message.NextRetryUtc = dateTimeProvider.UtcNow.AddMinutes(Math.Pow(_options.RetryBackoffBase, message.RetryCount));

            logger.LogWarning("Retry {RetryCount} of {MaxRetries} scheduled for outbox message {MessageId} at {NextRetryUtc}; skipping aggregate {AggregateId} for the rest of this run",
                message.RetryCount, _options.MaxRetries, message.Id, message.NextRetryUtc, message.AggregateId);
        }
    }

    private record PublishOutcome(OutboxMessage Message, string? Error);
}
