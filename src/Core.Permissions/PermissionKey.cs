namespace Core.Permissions;

internal static class PermissionKey
{
    public static string For(Enum permission) => $"{permission.GetType().FullName}.{permission}";
}