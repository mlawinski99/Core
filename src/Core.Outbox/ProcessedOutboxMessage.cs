namespace Core.Outbox;

public sealed class ProcessedOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public bool IsProcessed { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryUtc { get; set; }
    public DateTime? StoppedRetryingUtc { get; set; }
    public string? LastError { get; set; }
}