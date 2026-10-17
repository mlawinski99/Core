using Microsoft.AspNetCore.Authorization;

namespace Core.Permissions;

internal sealed class PermissionAuthorizationHandler(IPermissionChecker permissionChecker)
    : AuthorizationHandler<HasPermissionAttribute>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, HasPermissionAttribute requirement)
    {
        if (await permissionChecker.HasPermission(requirement.Permission))
            context.Succeed(requirement);
    }
}