using Core.IntegrationTests.Shared.Settings;
using Testcontainers.Kafka;

namespace Core.InfrastructureTests.Kafka.Containers;

public class KafkaContainerFixture : IAsyncDisposable
{
    private readonly KafkaContainer _container;

    public string BootstrapServers => _container.GetBootstrapAddress();

    public KafkaContainerFixture()
    {
        _container = new KafkaBuilder()
            .WithImage(ContainerImages.Kafka)
            .Build();
    }

    public Task StartAsync() => _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
