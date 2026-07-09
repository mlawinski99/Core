namespace Core.Infrastructure.Json;

public interface IJsonSerializer
{
    T Deserialize<T>(string value);
    string Serialize<T>(T value);
}