using Core.Logger;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Core.KafkaConsumer;

public class KafkaConsumer : IConsumer, IDisposable
{
    private readonly IAppLogger<KafkaConsumer> _logger;
    private readonly HashSet<string> _allowedTopics;
    private readonly IConsumer<string, string> _consumer;

    public KafkaConsumer(IOptions<KafkaConsumerConfiguration> configuration,
        IAppLogger<KafkaConsumer> logger)
    {
        _logger = logger;
        var kafkaConfig = configuration.Value;
        _allowedTopics = new HashSet<string>(kafkaConfig.AllowedTopics);

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaConfig.BootstrapServers,
            GroupId = kafkaConfig.GroupId,
            AutoOffsetReset = kafkaConfig.AutoOffsetReset.ToLower() == "latest"
                ? AutoOffsetReset.Latest
                : AutoOffsetReset.Earliest,
            EnableAutoCommit = kafkaConfig.EnableAutoCommit
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();

        _consumer.Subscribe(_allowedTopics.ToList());
    }

    public async Task StartAsync(
        Func<string, string, Task> handler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(cancellationToken);

                if (!_allowedTopics.Contains(result.Topic))
                    continue;

                _logger.LogInformation("Received message from topic {Topic} with key {Key}", result.Topic, result.Message.Key);
                await handler(result.Topic, result.Message.Value);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message handler failed; message is skipped");
            }
        }
    }

    public void Dispose() => _consumer.Close();
}