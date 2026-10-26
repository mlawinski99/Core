namespace Core.DomainTypes;

public abstract class DomainEventBase : IDomainEvent
{
    public DateTime OccurredOnUtc { get; }

    public virtual bool IsBlocking => false;

    protected DomainEventBase()
    {
        OccurredOnUtc = DateTime.UtcNow; //@TODO use time provider
    }
}
