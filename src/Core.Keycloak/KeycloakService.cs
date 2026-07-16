using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Infrastructure.Json;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Core.Keycloak;

public interface IKeycloakService
{
    Task<string> GetToken();
    Task<KeycloakUserDto?> GetUser(string token, string userId);
    Task CreateUser(string token, string username, string email, string password);
    Task UpdateUser(string token, string userId, string email);
    Task DeleteUser(string token, string userId);
    Task<KeycloakTokenResponse?> LoginUser(string username, string password);
    Task LogoutUser(string refreshToken);
}

public class KeycloakService : IKeycloakService
{
    private readonly IJsonSerializer _jsonSerializer;
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly KeycloakConfig _keycloakConfig;
    private string _tokenUrl => KeycloakEndpoints.TokenEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm);

    public KeycloakService(IHttpClientFactory httpClientFactory,
        IOptions<KeycloakConfig> keycloakConfig,
        IJsonSerializer jsonSerializer)
    {
        _jsonSerializer = jsonSerializer;
        _httpClient = httpClientFactory.CreateClient();
        _keycloakConfig = keycloakConfig.Value;
    }

    public async Task<string> GetToken()
    {
        var response = await _httpClient.PostAsync(
            _tokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _keycloakConfig.ClientId},
                {"client_secret", _keycloakConfig.ClientSecret},
                {"grant_type", "client_credentials"}
            }));

        await EnsureSuccessAsync(response, nameof(GetToken));
        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<JsonElement>(json).GetProperty("access_token").GetString();

        return token;
    }

    public async Task<KeycloakUserDto?> GetUser(string token, string userId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            KeycloakEndpoints.UserEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm, userId)
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var keycloakResponse = await _httpClient.SendAsync(request);

        if (!keycloakResponse.IsSuccessStatusCode)
            return null;

        var json = await keycloakResponse.Content.ReadAsStringAsync();
        var keycloakUser = _jsonSerializer.Deserialize<KeycloakUserDto>(json);

        return keycloakUser;
    }

    public async Task CreateUser(string token, string username, string email, string password)
    {
        var userJson = _jsonSerializer.Serialize(new
        {
            username = username,
            email = email,
            enabled = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false
                }
            }
        });

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            KeycloakEndpoints.UsersEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm))
        {
            Content = new StringContent(userJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response, nameof(CreateUser));
    }

    public async Task UpdateUser(string token, string userId, string email)
    {
        var userJson = _jsonSerializer.Serialize(new
        {
            email = email
        });

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            KeycloakEndpoints.UserEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm, userId))
        {
            Content = new StringContent(userJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response, nameof(UpdateUser));
    }

    public async Task DeleteUser(string token, string userId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            KeycloakEndpoints.UserEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm, userId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response, nameof(DeleteUser));
    }

    public async Task<KeycloakTokenResponse?> LoginUser(string username, string password)
    {
        var response = await _httpClient.PostAsync(
            _tokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _keycloakConfig.ClientId},
                {"client_secret", _keycloakConfig.ClientSecret},
                {"grant_type", "password"},
                {"username", username},
                {"password", password}
            }));

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            return null;

        await EnsureSuccessAsync(response, nameof(LoginUser));

        var json = await response.Content.ReadAsStringAsync();
        return _jsonSerializer.Deserialize<KeycloakTokenResponse>(json);
    }

    public async Task LogoutUser(string refreshToken)
    {
        var response = await _httpClient.PostAsync(
            KeycloakEndpoints.LogoutEndpoint(_keycloakConfig.AuthServerUrl, _keycloakConfig.Realm),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _keycloakConfig.ClientId},
                {"client_secret", _keycloakConfig.ClientSecret},
                {"refresh_token", refreshToken}
            }));

        await EnsureSuccessAsync(response, nameof(LogoutUser));
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorContent = await response.Content.ReadAsStringAsync();
        var message = $"Keycloak {operation} failed with code {response.StatusCode} and message {errorContent}";

        throw new KeycloakException(message, response.StatusCode);
    }
}
