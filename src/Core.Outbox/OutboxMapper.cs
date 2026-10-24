namespace Core.Outbox;

internal static class OutboxMapper
{
    public static DeadLetterOutboxMessage ToDeadLetter(OutboxMessage message, DateTime nowUtc) => new()
    {
        Id = message.Id,
        AggregateId = message.AggregateId,
        OccurredOnUtc = message.OccurredOnUtc,
        Type = message.Type,
        Content = message.Content,
        CorrelationId = message.CorrelationId,
        IsBlocking = message.IsBlocking,
        RetryCount = message.RetryCount,
        LastError = message.LastError,
        DeadLetteredOnUtc = nowUtc
    };

    public static ProcessedOutboxMessage ToProcessed(OutboxMessage message) => new()
    {
        Id = message.Id,
        AggregateId = message.AggregateId,
        OccurredOnUtc = message.OccurredOnUtc,
        Type = message.Type,
        Content = message.Content,
        CorrelationId = message.CorrelationId,
        IsBlocking = message.IsBlocking,
        ProcessedOn = message.ProcessedOn,
        Outcome = message.Outcome,
        RetryCount = message.RetryCount,
        NextRetryUtc = message.NextRetryUtc,
        LastError = message.LastError
    };
}