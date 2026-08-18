namespace Core.Keycloak;

// @TODO move to shared
public class KeycloakConfig
{
    public const string SectionName = "Keycloak";

    public required string AuthServerUrl { get; init; }
    public required string Realm { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}