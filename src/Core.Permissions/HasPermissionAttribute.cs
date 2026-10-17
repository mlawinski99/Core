using Microsoft.AspNetCore.Authorization;

namespace Core.Permissions;

public sealed class HasPermissionAttribute(object permission)
    : AuthorizeAttribute, IAuthorizationRequirement, IAuthorizationRequirementData
{
    internal Enum Permission { get; } = (Enum)permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [this];
}