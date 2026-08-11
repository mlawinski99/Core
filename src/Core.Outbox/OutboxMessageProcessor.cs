using Core.DateTimeProvider;
using Core.KafkaProducer;
using Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace Core.Outbox;

public class OutboxMessageProcessor<TContext> : IOutboxMessageProcessor<TContext>
    where TContext : DbContext, IOutbox
{
    private readonly TContext _db;
    private readonly IAppLogger<OutboxMessageProcessor<TContext>> _logger;
    private readonly IProducer<OutboxMessage> _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly int _batchSize = 100;
    private const int MaxRetries = 5;

    public OutboxMessageProcessor(TContext db,
        IAppLogger<OutboxMessageProcessor<TContext>> logger,
        IProducer<OutboxMessage> producer,
        IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _logger = logger;
        _producer = producer;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        var messages = await _db.OutboxMessages
            .Where(m => m.ProcessedOn == null
                        && m.StoppedRetryingUtc == null
                        && (m.NextRetryUtc == null || m.NextRetryUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        var skippedAggregates = new HashSet<Guid>();

        foreach (var message in messages)
        {
            if (skippedAggregates.Contains(message.AggregateId))
            {
                _logger.LogWarning("Skipping outbox message {MessageId}; an earlier message for aggregate {AggregateId} failed", message.Id, message.AggregateId);
                continue;
            }

            try
            {
                // @TODO batch publishing
                var isProduceSucceeded = await _producer.ProduceAsync(message.Type, message, message.AggregateId.ToString(), cancellationToken);

                if (isProduceSucceeded)
                {
                    message.ProcessedOn = _dateTimeProvider.UtcNow;
                    message.IsProcessed = true;

                    await _db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to produce outbox message {MessageId} to topic {Topic}", message.Id, message.Type);
                    await HandleFailureAsync(message, $"Producer returned false for topic {message.Type}", skippedAggregates, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                await HandleFailureAsync(message, ex.Message, skippedAggregates, cancellationToken);
            }
        }
    }

    private async Task HandleFailureAsync(OutboxMessage message, string error, HashSet<Guid> skippedAggregates, CancellationToken cancellationToken)
    {
        message.RetryCount++;
        message.LastError = error;

        if (message.RetryCount >= MaxRetries)
        {
            message.StoppedRetryingUtc = _dateTimeProvider.UtcNow;

            _logger.LogError("Outbox message {MessageId} of type {Type} for aggregate {AggregateId} parked after {RetryCount} attempts, correlation {CorrelationId}: {LastError}",
                message.Id, message.Type, message.AggregateId, message.RetryCount, message.CorrelationId ?? "NULL", error);
        }
        else
        {
            message.NextRetryUtc = _dateTimeProvider.UtcNow.AddMinutes(Math.Pow(2, message.RetryCount));
            skippedAggregates.Add(message.AggregateId);

            _logger.LogWarning("Retry {RetryCount} of {MaxRetries} scheduled for outbox message {MessageId} at {NextRetryUtc}; skipping aggregate {AggregateId} for the rest of this run",
                message.RetryCount, MaxRetries, message.Id, message.NextRetryUtc, message.AggregateId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
