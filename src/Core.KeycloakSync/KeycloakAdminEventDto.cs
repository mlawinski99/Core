using System.Text.Json.Serialization;

namespace Core.KeycloakSync;

public class KeycloakAdminEventDto
{
    [JsonPropertyName("operationType")]
    public string OperationType { get; set; }
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; }
    [JsonPropertyName("resourcePath")]
    public string ResourcePath { get; set; }
    [JsonPropertyName("time")]
    public long Time { get; set; }

    public KeycloakAdminEvent ToKeycloakEvent =>
        new KeycloakAdminEvent()
        {
            OperationType = OperationType,
            ResourceType = ResourceType,
            ResourcePath = ResourcePath,
            Time = DateTimeOffset.FromUnixTimeMilliseconds(Time).UtcDateTime
        };
}