using Core.Infrastructure.Json;
using Core.InfrastructureTests.Kafka.Fixtures;
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
