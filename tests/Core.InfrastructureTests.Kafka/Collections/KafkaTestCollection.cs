using Core.InfrastructureTests.Kafka.Fixtures;
using Xunit;

namespace Core.InfrastructureTests.Kafka.Collections;

[CollectionDefinition("Kafka")]
public class KafkaTestCollection : ICollectionFixture<KafkaTestFixture>
{
}