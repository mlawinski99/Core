using Core.DomainTypes;
using Core.Infrastructure;

namespace Core.IntegrationTests.Shared.Infrastructure.TestEntities;

public class EncryptableEntity : Entity
{
    public string Name { get; set; } = string.Empty;

    [Encryptable]
    public string Secret { get; set; } = string.Empty;
}
