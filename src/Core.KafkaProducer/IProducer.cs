namespace Core.KafkaProducer;

public interface IProducer<T>
{
    Task<bool> ProduceAsync(string topic, T message, string? key = null, CancellationToken cancellationToken = default);
}
