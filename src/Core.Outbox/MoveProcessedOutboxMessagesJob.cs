using Core.BackgroundJobs;
using Core.DateTimeProvider;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Outbox;

[DisallowConcurrentExecution]
public class MoveProcessedOutboxMessagesJob<TContext>(
    TContext db,
    IAppLogger<MoveProcessedOutboxMessagesJob<TContext>> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> options)
    : IBackgroundJob
    where TContext : DbContext, IOutbox
{
    public const string JobId = "move-processed-outbox-messages";

    private readonly OutboxOptions _options = options.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        var processedBeforeUtc = dateTimeProvider.UtcNow.AddDays(-_options.MoveAfterDays);
        var movedTotal = 0;

        while (true)
        {
            var messages = await db.OutboxMessages
                .Where(m => m.ProcessedOn != null && m.ProcessedOn < processedBeforeUtc)
                .OrderBy(m => m.ProcessedOn)
                .Take(_options.MoveBatchSize)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
                break;

            db.ProcessedOutboxMessages.AddRange(messages.Select(Map));
            db.OutboxMessages.RemoveRange(messages);

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();

            movedTotal += messages.Count;
        }

        logger.LogInformation("Moved {MovedCount} outbox messages processed before {ProcessedBeforeUtc} to ProcessedOutboxMessages", movedTotal, processedBeforeUtc);
    }

    private static ProcessedOutboxMessage Map(OutboxMessage message) => new()
    {
        Id = message.Id,
        AggregateId = message.AggregateId,
        OccurredOnUtc = message.OccurredOnUtc,
        Type = message.Type,
        Content = message.Content,
        CorrelationId = message.CorrelationId,
        ProcessedOn = message.ProcessedOn,
        IsProcessed = message.IsProcessed,
        RetryCount = message.RetryCount,
        NextRetryUtc = message.NextRetryUtc,
        StoppedRetryingUtc = message.StoppedRetryingUtc,
        LastError = message.LastError
    };
}
