using Core.DomainTypes;

namespace Core.IntegrationTests.Shared.Infrastructure.TestEntities;

public class TestEntity : AggregateRoot
{
    public string Name { get; set; } = string.Empty;

    public void RaiseCreatedEvent(DateTime nowUtc) => AddDomainEvent(new TestEntityCreatedEvent(Id, nowUtc));

    public void RaiseBlockingEvent(DateTime nowUtc) => AddDomainEvent(new TestEntityBlockingEvent(Id, nowUtc));
}

public record TestEntityCreatedEvent(Guid AggregateId, DateTime OccurredOnUtc) : IDomainEvent;

public record TestEntityBlockingEvent(Guid AggregateId, DateTime OccurredOnUtc) : IDomainEvent
{
    public bool IsBlocking => true;
}