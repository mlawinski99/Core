using Confluent.Kafka;
using Core.Json;
using Core.Logger;
using Microsoft.Extensions.Options;

namespace Core.KafkaProducer;

public class KafkaProducer<T> : IProducer<T>, IDisposable
{
    private readonly ProducerConfig _config;
    private readonly IAppLogger<KafkaProducer<T>> _logger;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly Lock _rebuildLock = new();
    private IProducer<string, string> _producer;

    public KafkaProducer(IOptions<KafkaProducerConfiguration> configuration,
        IAppLogger<KafkaProducer<T>> logger,
        IJsonSerializer jsonSerializer)
    {
        var kafkaConfig = configuration.Value;

        _config = new ProducerConfig
        {
            BootstrapServers = kafkaConfig.BootstrapServers,
            MessageTimeoutMs = kafkaConfig.MessageTimeoutMs,
            EnableIdempotence = true,
            Acks = Acks.All,
            Partitioner = Partitioner.Murmur2Random
        };

        _logger = logger;
        _jsonSerializer = jsonSerializer;
        _producer = BuildProducer();
    }

    public async Task<ProduceResult> ProduceAsync(string topic, T message, string? key, CancellationToken cancellationToken = default)
    {
        var messageKey = key ?? Guid.Empty.ToString();
        var producer = _producer;

        try
        {
            var serializedMessage = _jsonSerializer.Serialize(message);

            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = messageKey,
                Value = serializedMessage
            }, cancellationToken);

            _logger.LogInformation("Successfully delivered message {Key} to topic {Topic}", messageKey, topic);
            return new ProduceResult(ProduceStatus.Delivered);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KafkaException ex) when (ex.Error.IsFatal)
        {
            _logger.LogError(ex, "Fatal producer error delivering message {Key} to topic {Topic}; rebuilding the Kafka client", messageKey, topic);
            RebuildProducer(producer);
            return new ProduceResult(ProduceStatus.Unavailable, ex.Error.Reason);
        }
        catch (KafkaException ex)
        {
            var status = ex.Error.Code == ErrorCode.Local_MsgTimedOut ? ProduceStatus.Unavailable : ProduceStatus.Rejected;

            _logger.LogError(ex, "Failed to deliver message {Key} to topic {Topic}: {Status} ({ErrorCode})", messageKey, topic, status, ex.Error.Code);
            return new ProduceResult(status, ex.Error.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver message {Key} to topic {Topic}", messageKey, topic);
            return new ProduceResult(ProduceStatus.Rejected, ex.Message);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }

    private IProducer<string, string> BuildProducer() => new ProducerBuilder<string, string>(_config).Build();

    private void RebuildProducer(IProducer<string, string> faulted)
    {
        lock (_rebuildLock)
        {
            if (_producer != faulted)
                return;

            _producer = BuildProducer();
        }

        faulted.Flush(TimeSpan.FromSeconds(5));
        faulted.Dispose();
    }
}
