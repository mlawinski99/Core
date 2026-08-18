namespace Core.KafkaConsumer;

// @TODO analyze settings and set it up correctly
public class KafkaConsumerConfiguration
{
    public const string SectionName = "Kafka:Consumer";

    public required string BootstrapServers { get; init; }
    public required string GroupId { get; init; }
    public required List<string> AllowedTopics { get; init; }
    public string AutoOffsetReset { get; init; } = "earliest";
    public bool EnableAutoCommit { get; init; } = true;
}
