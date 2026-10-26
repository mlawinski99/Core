namespace Core.Outbox.Models;

public record DeadLetterListItem(
    Guid Id,
    string Type,
    Guid AggregateId,
    bool IsBlocking,
    int RetryCount,
    string? LastError,
    DateTime DeadLetteredOnUtc);