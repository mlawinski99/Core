namespace Core.KeycloakSync;

public class KeycloakSyncOptions
{
    public const string SectionName = "KeycloakSync";

    public string Cron { get; init; } = "*/5 * * * *";
}
