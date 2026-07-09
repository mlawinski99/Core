namespace Core.Keycloak;

public static class KeycloakEndpoints
{
    public static string HttpClientName = "Keycloak";
    public static string TokenEndpoint(string url, string realm) => $"{url}/realms/{realm}/protocol/openid-connect/token";
    public static string UsersEndpoint(string url, string realm) => $"{url}/admin/realms/{realm}/users";
    public static string UserEndpoint(string url, string realm, string userId) => $"{url}/admin/realms/{realm}/users/{userId}";
    public static string LogoutEndpoint(string url, string realm) => $"{url}/realms/{realm}/protocol/openid-connect/logout";
}