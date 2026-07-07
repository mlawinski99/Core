using Core.DateTimeProvider;
using Core.Logger;
using Core.KafkaProducer;
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
        var messages = await _db.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                // @TODO batch publishing
                var isProduceSucceeded = await _producer.ProduceAsync(message.Type, message, message.CorrelationId, cancellationToken);

                if (isProduceSucceeded)
                {
                    message.ProcessedOn = _dateTimeProvider.UtcNow;
                    message.IsProcessed = true;

                    await _db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to produce outbox message {MessageId} to topic {Topic}; stopping batch to preserve ordering", message.Id, message.Type);
                    _db.Entry(message).State = EntityState.Unchanged;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}; stopping batch to preserve ordering", message.Id);
                _db.Entry(message).State = EntityState.Unchanged;
            }
        }
    }
}
