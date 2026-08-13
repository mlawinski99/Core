using Core.DateTimeProvider;
using Core.KafkaProducer;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Outbox;

public class OutboxMessageProcessor<TContext>(
    TContext db,
    IAppLogger<OutboxMessageProcessor<TContext>> logger,
    IProducer<OutboxMessage> producer,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> options)
    : IOutboxMessageProcessor<TContext>
    where TContext : DbContext, IOutbox
{
    private readonly OutboxOptions _options = options.Value;

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        // we want to block aggregate if any message fails
        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var skippedAggregates = new HashSet<Guid>();

        foreach (var message in messages)
        {
            if (skippedAggregates.Contains(message.AggregateId))
            {
                logger.LogWarning("Skipping outbox message {MessageId}; an earlier message for aggregate {AggregateId} is still unpublished", message.Id, message.AggregateId);
                continue;
            }

            if (message.StoppedRetryingUtc is not null || message.NextRetryUtc > now)
            {
                skippedAggregates.Add(message.AggregateId);
                continue;
            }

            try
            {
                // @TODO batch publishing
                var isProduceSucceeded = await producer.ProduceAsync(message.Type, message, message.AggregateId.ToString(), cancellationToken);

                if (isProduceSucceeded)
                {
                    message.ProcessedOn = dateTimeProvider.UtcNow;
                    message.IsProcessed = true;

                    await db.SaveChangesAsync(cancellationToken);

                    logger.LogInformation("Published outbox message {MessageId} of type {Type}, correlation {CorrelationId}",
                        message.Id, message.Type, message.CorrelationId ?? "NULL");
                }
                else
                {
                    logger.LogError("Failed to produce outbox message {MessageId} to topic {Topic}", message.Id, message.Type);
                    await HandleFailureAsync(message, $"Producer returned false for topic {message.Type}", skippedAggregates, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                await HandleFailureAsync(message, ex.Message, skippedAggregates, cancellationToken);
            }
        }
    }

    private async Task HandleFailureAsync(OutboxMessage message, string error, HashSet<Guid> skippedAggregates, CancellationToken cancellationToken)
    {
        message.RetryCount++;
        message.LastError = error;
        skippedAggregates.Add(message.AggregateId);

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

        await db.SaveChangesAsync(cancellationToken);
    }
}
