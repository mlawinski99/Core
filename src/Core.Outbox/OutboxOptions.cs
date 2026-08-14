namespace Core.Outbox;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 100;
    public int MaxRetries { get; init; } = 5;
    public double RetryBackoffBase { get; init; } = 2;
    public int MoveAfterDays { get; init; } = 7;
    public int MoveBatchSize { get; init; } = 1000;
}