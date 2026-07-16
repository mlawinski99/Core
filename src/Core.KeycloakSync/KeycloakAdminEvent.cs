namespace Core.KeycloakSync;

public class KeycloakAdminEvent
{
    public long Id { get; set; }
    public string OperationType { get; set; }
    public string ResourceType { get; set; }
    public string ResourcePath { get; set; }
    public DateTime Time { get; set; }
    public bool IsProcessed { get; set; }
}
