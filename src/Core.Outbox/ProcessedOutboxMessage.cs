namespace Core.Outbox;

public sealed class ProcessedOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string? CorrelationId { get; set; }
    public bool IsBlocking { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public OutboxMessageOutcome? Outcome { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryUtc { get; set; }
    public string? LastError { get; set; }
}