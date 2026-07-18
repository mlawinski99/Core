using Core.DomainTypes;

namespace Core.IntegrationTests.Shared.Infrastructure.TestEntities;

public class TestEntity : AggregateRoot
{
    public string Name { get; set; } = string.Empty;

    public void RaiseCreatedEvent() => AddDomainEvent(new TestEntityCreatedEvent(Id, DateTime.UtcNow));
}

public record TestEntityCreatedEvent(Guid AggregateId, DateTime OccurredOnUtc) : IDomainEvent;