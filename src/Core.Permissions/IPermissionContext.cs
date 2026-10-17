using Core.Permissions.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Permissions;

public interface IPermissionContext
{
    DbSet<UserPermission> UserPermissions { get; set; }
    DbSet<Role> Roles { get; set; }
    DbSet<RolePermission> RolePermissions { get; set; }
    DbSet<UserRole> UserRoles { get; set; }
}
