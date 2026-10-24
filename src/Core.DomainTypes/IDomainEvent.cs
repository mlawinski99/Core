namespace Core.DomainTypes;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
    bool IsBlocking { get; }
}