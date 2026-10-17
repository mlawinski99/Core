namespace Core.Permissions;

public interface IPermissionChecker
{
    Task<bool> HasPermission(Enum permission, CancellationToken cancellationToken = default);
}