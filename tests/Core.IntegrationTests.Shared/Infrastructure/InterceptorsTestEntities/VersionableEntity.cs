using Core.DomainTypes;

namespace Core.IntegrationTests.Shared.Infrastructure.TestEntities;

public class VersionableEntity : Entity, IVersionable
{
    public string Name { get; set; } = string.Empty;
    public int VersionId { get; set; }
    public Guid? VersionGroupId { get; set; }
}
