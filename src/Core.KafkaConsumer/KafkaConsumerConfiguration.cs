namespace Core.KafkaConsumer;

public class KafkaConsumerConfiguration
{
    public string BootstrapServers { get; set; }
    public string GroupId { get; set; }
    public List<string> AllowedTopics { get; set; }
    public string AutoOffsetReset { get; set; }
    public bool EnableAutoCommit { get; set; }
}
