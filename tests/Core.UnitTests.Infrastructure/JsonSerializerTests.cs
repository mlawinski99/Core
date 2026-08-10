using Core.Infrastructure.Json;
using FluentAssertions;
using Xunit;

namespace Core.UnitTests.Infrastructure;

public class JsonSerializerTests
{
    private readonly JsonSerializer _serializer = new();

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Serialize_ShouldUsePropertyNamesUnchanged()
    {
        // Arrange
        var obj = new TestObject { Id = 1, Name = "Test" };

        // Act
        var json = _serializer.Serialize(obj);

        // Assert
        json.Should().Be("{\"Id\":1,\"Name\":\"Test\"}");
    }

    [Fact]
    public void Deserialize_ShouldReadPropertyNamesUnchanged()
    {
        // Arrange
        var json = "{\"Id\":1,\"Name\":\"Test\"}";

        // Act
        var obj = _serializer.Deserialize<TestObject>(json);

        // Assert
        obj.Id.Should().Be(1);
        obj.Name.Should().Be("Test");
    }
}
