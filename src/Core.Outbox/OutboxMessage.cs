namespace Core.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public bool IsProcessed { get; set; }
}
