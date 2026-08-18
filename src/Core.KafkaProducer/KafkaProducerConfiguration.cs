namespace Core.KafkaProducer;

// @TODO analyze settings and set it up correctly
public class KafkaProducerConfiguration
{
    public const string SectionName = "Kafka:Producer";

    public required string BootstrapServers { get; init; }
    public bool EnableIdempotence { get; init; } = true;
    public int MessageTimeoutMs { get; init; } = 30000;
    public string Acks { get; init; } = "all";
}
