using Confluent.Kafka;
using Core.Logger;
using Microsoft.Extensions.Options;

namespace Core.KafkaConsumer;

public class KafkaConsumer : IConsumer, IDisposable
{
    private readonly IAppLogger<KafkaConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;

    public KafkaConsumer(IOptions<KafkaConsumerConfiguration> configuration,
        IAppLogger<KafkaConsumer> logger)
    {
        _logger = logger;
        var kafkaConfig = configuration.Value;
        var allowedTopics = new HashSet<string>(kafkaConfig.AllowedTopics);

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

        _consumer.Subscribe(allowedTopics.ToList());
    }

    public async Task StartAsync(
        Func<string, string, Task> handler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string> result;

            try
            {
                result = _consumer.Consume(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kafka consume failed");
                throw;
            }

            _logger.LogInformation(
                "Received message from topic {Topic} partition {Partition} offset {Offset} with key {Key}",
                result.Topic, result.Partition.Value, result.Offset.Value, result.Message.Key);

            await handler(result.Topic, result.Message.Value);
        }

        _logger.LogInformation("Kafka consumer stopping");
    }

        public void Dispose()
    {
        try
        {
            _consumer.Close();
        }
        finally
        {
            _consumer.Dispose();
        }
    }
}
