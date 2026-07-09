using Core.Infrastructure.Json;
using Core.InfrastructureTests.Kafka.Fixtures;
using FluentAssertions;
using Xunit;

namespace Core.InfrastructureTests.Kafka;

[Collection("Kafka")]
public class KafkaTests
{
    private readonly KafkaTestFixture _fixture;

    public KafkaTests(KafkaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProduceAsync_WithValidMessage_ShouldReturnTrue()
    {
        // Arrange
        var topic = "test-topic";
        var message = new TestMessage { Id = 1, Content = "Test" };
        using var producer = _fixture.CreateProducer<TestMessage>();

        // Act
        var result = await producer.ProduceAsync(topic, message);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProduceAsync_MessageCanBeConsumedByConsumer()
    {
        // Arrange
        var topic = "producer-consumer-topic";
        var message = new TestMessage { Id = 3, Content = "Test" };
        using var producer = _fixture.CreateProducer<TestMessage>();
        var jsonSerializer = _fixture.CreateJsonSerializer();

        string? receivedValue = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        await producer.ProduceAsync(topic, message);

        using var consumer = _fixture.CreateConsumer(topics: new List<string> { topic });
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
    public async Task ProduceAsync_MultipleMessages_ShouldAllBeProduced()
    {
        // Arrange
        var topic = "multi-topic";
        using var producer = _fixture.CreateProducer<TestMessage>();

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var message = new TestMessage { Id = i, Content = $"Message {i}" };
            var result = await producer.ProduceAsync(topic, message);
            result.Should().BeTrue();
        }
    }

    [Fact]
    public async Task StartAsync_ShouldInvokeHandlerForEachMessage()
    {
        // Arrange
        var topic = "invoke-topic";
        using var producer = _fixture.CreateProducer<TestMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var receivedMessages = new List<string>();
        var expectedCount = 3;

        for (int i = 0; i < expectedCount; i++)
        {
            await producer.ProduceAsync(topic, new TestMessage { Id = i, Content = $"Test {i}" });
        }

        // Act
        using var consumer = _fixture.CreateConsumer(topics: new List<string> { topic });
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
        using var producer = _fixture.CreateProducer<TestMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var receivedTopics = new HashSet<string>();

        await producer.ProduceAsync(topic1, new TestMessage { Id = 1, Content = "Test 1" });
        await producer.ProduceAsync(topic2, new TestMessage { Id = 2, Content = "Test 2" });

        // Act
        using var consumer = _fixture.CreateConsumer(topics: new List<string> { topic1, topic2 });
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