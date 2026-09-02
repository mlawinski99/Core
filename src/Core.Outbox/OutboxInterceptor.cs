using System.Diagnostics;
using Core.DomainTypes;
using Core.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.Outbox;

public class OutboxInterceptor(IJsonSerializer jsonSerializer) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StageOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StageOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // events are cleared as they are staged, so a retried or second save writes the rows already staged
    private void StageOutboxMessages(DbContext? context)
    {
        if (context is not IOutbox outboxContext)
            return;

        var correlationId = Activity.Current?.TraceId.ToString();

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(x => x.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            // serialize every event before tracking any, so a failure part-way stages nothing for this aggregate
            var messages = aggregate.DomainEvents
                .Select(domainEvent => new OutboxMessage
                {
                    AggregateId = aggregate.Id,
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    Type = domainEvent.GetType().FullName!,
                    Content = jsonSerializer.Serialize(domainEvent),
                    CorrelationId = correlationId
                })
                .ToList();

            outboxContext.OutboxMessages.AddRange(messages);
            aggregate.ClearDomainEvents();
        }
    }
}
