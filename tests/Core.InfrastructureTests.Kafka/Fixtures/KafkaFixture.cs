using Core.Infrastructure.Json;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Settings;
using Core.KafkaConsumer;
using Core.KafkaProducer;
using Core.Logger;
using Microsoft.Extensions.Options;
using NSubstitute;
using Testcontainers.Kafka;
using Xunit;

namespace Core.InfrastructureTests.Kafka.Fixtures;

public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder()
        .WithImage(ContainerImages.Kafka)
        .Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public KafkaProducer<T> CreateProducer<T>(KafkaProducerConfiguration? config = null)
    {
        var configuration = config ?? new KafkaProducerConfiguration
        {
            BootstrapServers = BootstrapServers,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000,
            Acks = "all"
        };

        return new KafkaProducer<T>(
            Options.Create(configuration),
            Substitute.For<IAppLogger<KafkaProducer<T>>>(),
            new TestJsonSerializer());
    }

    public KafkaConsumer.KafkaConsumer CreateConsumer(KafkaConsumerConfiguration? config = null, List<string>? topics = null)
    {
        var configuration = config ?? new KafkaConsumerConfiguration
        {
            BootstrapServers = BootstrapServers,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AllowedTopics = topics ?? ["test-topic"],
            AutoOffsetReset = "earliest",
            EnableAutoCommit = true
        };

        return new KafkaConsumer.KafkaConsumer(
            Options.Create(configuration),
            Substitute.For<IAppLogger<KafkaConsumer.KafkaConsumer>>());
    }

    public IJsonSerializer CreateJsonSerializer() => new TestJsonSerializer();
}