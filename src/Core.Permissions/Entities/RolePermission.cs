namespace Core.Permissions.Entities;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; }
}
