namespace Core.KafkaConsumer;

public interface IConsumer
{
    Task StartAsync(Func<string, string, Task> handler, CancellationToken cancellationToken);
}