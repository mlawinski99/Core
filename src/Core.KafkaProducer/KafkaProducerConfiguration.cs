namespace Core.KafkaProducer;

public class KafkaProducerConfiguration
{
    public const string SectionName = "Kafka:Producer";

    public required string BootstrapServers { get; init; }
    public int MessageTimeoutMs { get; init; } = 30000;
}