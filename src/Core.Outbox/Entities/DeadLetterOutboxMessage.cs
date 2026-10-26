namespace Core.Outbox.Entities;

public sealed class DeadLetterOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string? CorrelationId { get; set; }
    public bool IsBlocking { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime DeadLetteredOnUtc { get; set; }
}