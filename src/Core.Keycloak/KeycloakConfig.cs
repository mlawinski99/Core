namespace Core.Keycloak;

// @TODO move to shared
public class KeycloakConfig
{
    public string AuthServerUrl { get; set; }
    public string Realm { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}