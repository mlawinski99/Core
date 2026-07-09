namespace Core.Outbox;

public interface IOutboxMessageProcessor<TContext>
{
    Task ProcessAsync(CancellationToken cancellationToken);
}