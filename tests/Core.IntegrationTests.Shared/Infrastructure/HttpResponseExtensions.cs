using Core.Infrastructure.Json;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Core.IntegrationTests.Shared.Infrastructure;

public static class HttpResponseExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ApiResponse> ReadResult(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(content, JsonOptions)
               ?? throw new JsonException($"Failed to deserialize response: {content}");
    }

    public static async Task<ApiResponse<T>> ReadResult<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions)
               ?? throw new JsonException($"Failed to deserialize response: {content}");
    }
}

/// <summary>
/// Result response for tests
/// </summary>
public record ApiResponse(bool IsSuccess, string? Error, int Code);

public record ApiResponse<T>(bool IsSuccess, string? Error, int Code, T? Data) : ApiResponse(IsSuccess, Error, Code);