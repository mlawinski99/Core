namespace Core.KafkaProducer;

public sealed record ProduceResult(ProduceStatus Status, string? Error = null);