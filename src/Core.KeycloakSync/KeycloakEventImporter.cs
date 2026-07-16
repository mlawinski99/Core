using System.Net.Http.Headers;
using Core.Infrastructure.Configuration;
using Core.Infrastructure.Json;
using Core.Keycloak;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.KeycloakSync;

public class KeycloakEventImporter<TContext> where TContext : DbContext, IKeycloakEventsContext, IConfigurationContext
{
    private readonly TContext _db;
    private readonly IAppLogger<KeycloakEventImporter<TContext>> _logger;
    private readonly IKeycloakService _keycloakService;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly HttpClient _httpClient;
    private readonly string _requestUrl;

    public KeycloakEventImporter(TContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakConfig> config,
        IAppLogger<KeycloakEventImporter<TContext>> logger,
        IKeycloakService keycloakService,
        IJsonSerializer jsonSerializer)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient(KeycloakEndpoints.HttpClientName);
        _logger = logger;
        _keycloakService = keycloakService;
        _jsonSerializer = jsonSerializer;

        _requestUrl = $"{config.Value.AuthServerUrl}/admin/realms/{config.Value.Realm}/admin-events" +
                      "?resourceTypes=USER&operationTypes=CREATE&operationTypes=UPDATE&operationTypes=DELETE";
    }

    public async Task ImportEventsAsync()
    {
        string responseBody = string.Empty;
        try
        {
            var keycloakLastSync = await _db.ConfigurationData
                .FirstOrDefaultAsync(c => c.Key == KeycloakSyncStaticSettings.SyncJobKeyValue);

            _logger.LogInformation("Fetched last sync time: {LastSync}", keycloakLastSync?.Value ?? "null");

            var url = _requestUrl;
            if (!string.IsNullOrEmpty(keycloakLastSync?.Value))
            {
                // @TODO keycloak 27 - Epoch timestamp millis
                var parsedDate = DateTime.Parse(keycloakLastSync.Value);
                var dateOnly = parsedDate.ToUniversalTime().ToString("yyyy-MM-dd");
                url += $"&dateFrom={dateOnly}";
            }

            var token = await _keycloakService.GetToken();

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(req);
            responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var events = _jsonSerializer.Deserialize<List<KeycloakAdminEventDto>>(responseBody);
            if (events is { Count: > 0 })
            {
                // @TODO keycloak 27 - if filter will work in query remove this filter
                DateTime? parsed = keycloakLastSync is not null
                    ? DateTime.Parse(keycloakLastSync.Value).ToUniversalTime()
                    : null;

                events.RemoveAll(e =>
                    (parsed.HasValue && e.Time < new DateTimeOffset(parsed.Value).ToUnixTimeMilliseconds()) ||
                    e.ResourceType != "USER" ||
                    (e.OperationType != "CREATE" &&
                     e.OperationType != "UPDATE" &&
                     e.OperationType != "DELETE"));
            }
            else
            {
                events = new List<KeycloakAdminEventDto>();
            }

            if (events.Count == 0)
            {
                _logger.LogInformation("No new Keycloak admin events found");
                return;
            }

            _db.KeycloakAdminEvents.AddRange(events.Select(x => x.ToKeycloakEvent));

            var maxEventTime = events.Max(e =>
                DateTimeOffset.FromUnixTimeMilliseconds(e.Time).UtcDateTime.AddMilliseconds(1));
            if (keycloakLastSync is null)
            {
                keycloakLastSync = new ConfigurationData { Key = KeycloakSyncStaticSettings.SyncJobKeyValue };
                _db.ConfigurationData.Add(keycloakLastSync);
            }
            keycloakLastSync.Value = maxEventTime.ToString("o");

            await _db.SaveChangesAsync();

            _logger.LogInformation("Keycloak admin event synchronization completed, {Count} events staged", events.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("HTTP error during Keycloak event synchronization, StatusCode: {StatusCode}, Response: {Response}",
                ex.StatusCode, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during Keycloak event synchronization: {ex}", ex);
        }
    }
}
