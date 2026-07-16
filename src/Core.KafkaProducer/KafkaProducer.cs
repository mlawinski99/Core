using Confluent.Kafka;
using Core.Infrastructure.Json;
using Core.Logger;
using Microsoft.Extensions.Options;

namespace Core.KafkaProducer;

public class KafkaProducer<T> : IProducer<T>, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IAppLogger<KafkaProducer<T>> _logger;
    private readonly IJsonSerializer _jsonSerializer;

    public KafkaProducer(IOptions<KafkaProducerConfiguration> configuration,
        IAppLogger<KafkaProducer<T>> logger,
        IJsonSerializer jsonSerializer)
    {
        var kafkaConfig = configuration.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaConfig.BootstrapServers,
            EnableIdempotence = kafkaConfig.EnableIdempotence,
            MessageTimeoutMs = kafkaConfig.MessageTimeoutMs,
            Acks = kafkaConfig.Acks.ToLowerInvariant() switch
            {
                "none" => Acks.None,
                "leader" => Acks.Leader,
                _ => Acks.All
            }
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger = logger;
        _jsonSerializer = jsonSerializer;
    }

    public async Task<bool> ProduceAsync(string topic, T message, string? key = null, CancellationToken cancellationToken = default)
    {
        var serializedMessage = _jsonSerializer.Serialize(message);
        var messageKey = key ?? Guid.NewGuid().ToString();

        try
        {
            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = messageKey,
                Value = serializedMessage
            }, cancellationToken);

            _logger.LogInformation("Successfully delivered message {Key} to topic {Topic}", messageKey, topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver message {Key} to topic {Topic}", messageKey, topic);
            return false;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
