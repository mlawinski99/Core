namespace Core.Outbox;

public enum OutboxMessageOutcome
{
    Published,
    Outdated,
    Discarded
}