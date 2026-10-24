using Core.Json;
using Core.InfrastructureTests.Kafka.Fixtures;
using Core.KafkaProducer;
using FluentAssertions;
using Xunit;

namespace Core.InfrastructureTests.Kafka;

[Collection("Kafka")]
public class KafkaTests(KafkaFixture kafkaFixture)
{
    [Fact]
    public async Task ProduceAsync_MessageCanBeConsumedByConsumer()
    {
        // Arrange
        var topic = "producer-consumer-topic";
        var message = new TestMessage { Id = 3, Content = "Test" };
        using var producer = kafkaFixture.CreateProducer<TestMessage>();
        var jsonSerializer = kafkaFixture.CreateJsonSerializer();

        string? receivedValue = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        await producer.ProduceAsync(topic, message, key: null);

        using var consumer = kafkaFixture.CreateConsumer(topics: new List<string> { topic });
        var consumeTask = consumer.StartAsync((_, v) =>
        {
            receivedValue = v;
            cts.Cancel();
            return Task.CompletedTask;
        }, cts.Token);

        await consumeTask;

        // Assert
        receivedValue.Should().NotBeNullOrEmpty();

        var deserializedMessage = jsonSerializer.Deserialize<TestMessage>(receivedValue!);
        deserializedMessage.Id.Should().Be(3);
        deserializedMessage.Content.Should().Be("Test");
    }

    [Fact]
    public async Task ProduceAsync_WhenCancelled_ShouldThrowInsteadOfReportingFailure()
    {
        // Arrange
        using var producer = kafkaFixture.CreateProducer<TestMessage>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await producer.ProduceAsync("cancelled-topic", new TestMessage { Id = 1, Content = "Test" }, "key", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProduceAsync_WithReachableBroker_ShouldReturnDelivered()
    {
        // Arrange
        using var producer = kafkaFixture.CreateProducer<TestMessage>();

        // Act
        var result = await producer.ProduceAsync("delivered-topic", new TestMessage { Id = 1, Content = "Test" }, "key");

        // Assert
        result.Status.Should().Be(ProduceStatus.Delivered);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProduceAsync_WithOversizedMessage_ShouldReturnRejected()
    {
        // Arrange
        using var producer = kafkaFixture.CreateProducer<TestMessage>();
        var message = new TestMessage { Id = 1, Content = new string('x', 2 * 1024 * 1024) };

        // Act
        var result = await producer.ProduceAsync("oversized-topic", message, "key");

        // Assert
        result.Status.Should().Be(ProduceStatus.Rejected);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProduceAsync_WithUnreachableBroker_ShouldReturnUnavailable()
    {
        // Arrange
        using var producer = kafkaFixture.CreateProducer<TestMessage>(new KafkaProducerConfiguration
        {
            BootstrapServers = "localhost:1",
            MessageTimeoutMs = 1000
        });

        // Act
        var result = await producer.ProduceAsync("unreachable-topic", new TestMessage { Id = 1, Content = "Test" }, "key");

        // Assert
        result.Status.Should().Be(ProduceStatus.Unavailable);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartAsync_ShouldInvokeHandlerForEachMessage()
    {
        // Arrange
        var topic = "invoke-topic";
        using var producer = kafkaFixture.CreateProducer<TestMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var receivedMessages = new List<string>();
        var expectedCount = 3;

        for (int i = 0; i < expectedCount; i++)
        {
            await producer.ProduceAsync(topic, new TestMessage { Id = i, Content = $"Test {i}" }, key: null);
        }

        // Act
        using var consumer = kafkaFixture.CreateConsumer(topics: new List<string> { topic });
        var consumeTask = consumer.StartAsync((_, v) =>
        {
            receivedMessages.Add(v);
            if (receivedMessages.Count >= expectedCount)
            {
                cts.Cancel();
            }
            return Task.CompletedTask;
        }, cts.Token);

        await consumeTask;

        // Assert
        receivedMessages.Count.Should().Be(expectedCount);
    }

    [Fact]
    public async Task StartAsync_WithMultipleTopics_ShouldConsumeFromAllTopics()
    {
        // Arrange
        var topic1 = "multi-topic-1";
        var topic2 = "multi-topic-2";
        using var producer = kafkaFixture.CreateProducer<TestMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var receivedTopics = new HashSet<string>();

        await producer.ProduceAsync(topic1, new TestMessage { Id = 1, Content = "Test 1" }, key: null);
        await producer.ProduceAsync(topic2, new TestMessage { Id = 2, Content = "Test 2" }, key: null);

        // Act
        using var consumer = kafkaFixture.CreateConsumer(topics: new List<string> { topic1, topic2 });
        var consumeTask = consumer.StartAsync((t, _) =>
        {
            receivedTopics.Add(t);
            if (receivedTopics.Count >= 2)
            {
                cts.Cancel();
            }
            return Task.CompletedTask;
        }, cts.Token);

        await consumeTask;

        // Assert
        receivedTopics.Should().Contain(topic1);
        receivedTopics.Should().Contain(topic2);
    }
}
