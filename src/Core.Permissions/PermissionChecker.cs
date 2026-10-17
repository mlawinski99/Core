using Core.RequestContext;
using Microsoft.EntityFrameworkCore;

namespace Core.Permissions;

internal sealed class PermissionChecker(IPermissionContext context, IUserProvider userProvider) : IPermissionChecker
{
    public async Task<bool> HasPermission(Enum permission, CancellationToken cancellationToken = default)
    {
        if (userProvider.UserId is null)
            return false;

        var permissionName = PermissionKey.For(permission);

        return await context.UserPermissions.AnyAsync(p => p.UserId == userProvider.UserId && p.Permission == permissionName, cancellationToken)
               || await context.UserRoles.AnyAsync(ur =>
                   ur.UserId == userProvider.UserId && context.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && rp.Permission == permissionName), cancellationToken);
    }
}