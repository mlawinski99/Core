namespace Core.Outbox.Entities;

public enum OutboxMessageOutcome
{
    Published,
    Outdated,
    Discarded
}