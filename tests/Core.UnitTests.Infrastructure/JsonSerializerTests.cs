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
    public void Serialize_ShouldReturnValidJson()
    {
        // Arrange
        var obj = new TestObject { Id = 1, Name = "Test" };

        // Act
        var json = _serializer.Serialize(obj);

        // Assert
        json.Should().Be("{\"Id\":1,\"Name\":\"Test\"}");
    }

    [Fact]
    public void Deserialize_ShouldReturnOriginalObject()
    {
        // Arrange
        var json = "{\"Id\":1,\"Name\":\"Test\"}";

        // Act
        var obj = _serializer.Deserialize<TestObject>(json);

        // Assert
        obj.Id.Should().Be(1);
        obj.Name.Should().Be("Test");
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrow()
    {
        // Arrange
        var invalidJson = "invalid";

        // Act
        var act = () => _serializer.Deserialize<TestObject>(invalidJson);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(-1, "-1")]
    [InlineData(0, "0")]
    public void SerializeDeserialize_Int_ShouldPass(int value, string expectedJson)
    {
        var json = _serializer.Serialize(value);
        json.Should().Be(expectedJson);

        var result = _serializer.Deserialize<int>(json);
        result.Should().Be(value);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("")]
    [InlineData("test text")]
    public void SerializeDeserialize_String_ShouldPass(string value)
    {
        var json = _serializer.Serialize(value);

        var result = _serializer.Deserialize<string>(json);
        result.Should().Be(value);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void SerializeDeserialize_Bool_ShouldPass(bool value, string expectedJson)
    {
        var json = _serializer.Serialize(value);
        json.Should().Be(expectedJson);

        var result = _serializer.Deserialize<bool>(json);
        result.Should().Be(value);
    }

    [Fact]
    public void SerializeDeserialize_Array_ShouldWork()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };

        // Act
        var json = _serializer.Serialize(array);
        var result = _serializer.Deserialize<int[]>(json);

        // Assert
        json.Should().Be("[1,2,3]");
        result.Should().BeEquivalentTo(array);
    }

    [Fact]
    public void SerializeDeserialize_List_ShouldWork()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c" };

        // Act
        var json = _serializer.Serialize(list);
        var result = _serializer.Deserialize<List<string>>(json);

        // Assert
        json.Should().Be("[\"a\",\"b\",\"c\"]");
        result.Should().BeEquivalentTo(list);
    }
}
