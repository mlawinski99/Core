namespace Core.DomainTypes;

public abstract class DomainEventBase : IDomainEvent
{
    public DateTime OccurredOnUtc { get; }

    protected DomainEventBase()
    {
        OccurredOnUtc = DateTime.UtcNow;
    }
}