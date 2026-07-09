using Core.DomainTypes;

namespace Core.IntegrationTests.Shared.Infrastructure.TestEntities;

public class SoftDeletableEntity : Entity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DateDeletedUtc { get; set; }
    public bool IsDeleted { get; set; }
}
