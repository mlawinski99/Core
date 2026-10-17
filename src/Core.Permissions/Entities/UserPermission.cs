namespace Core.Permissions.Entities;

public sealed class UserPermission
{
    public Guid UserId { get; set; }
    public string Permission { get; set; }
}
