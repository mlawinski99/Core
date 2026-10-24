namespace Core.KafkaProducer;

public interface IProducer<T>
{
    Task<ProduceResult> ProduceAsync(string topic, T message, string? key, CancellationToken cancellationToken = default);
}
