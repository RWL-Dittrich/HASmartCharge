using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.HomeAssistant.Services;

public class HomeAssistantControl : IHomeAssistantControl
{
    private readonly IHomeAssistantConnectionManager _connectionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeAssistantControl> _logger;

    public HomeAssistantControl(
        IHomeAssistantConnectionManager connectionManager,
        IHttpClientFactory httpClientFactory,
        ILogger<HomeAssistantControl> logger)
    {
        _connectionManager = connectionManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<double?> GetBatterySocAsync(string entityId, TimeSpan? maxAge = null, CancellationToken ct = default)
    {
        var reading = await GetEntityStateAsync(entityId, ct);
        var state = reading?.State;
        if (state == null)
        {
            return null;
        }

        if (state.Equals("unavailable", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!double.TryParse(state, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            _logger.LogWarning("Could not parse state '{State}' for entity {EntityId} as a battery SoC value", state, entityId);
            return null;
        }

        // Callers that measure something against the SoC (efficiency calibration) can't use a
        // reading the car reported long ago — while charging, the real SoC has moved on since.
        // No timestamp at all means an HA that doesn't report one: accept rather than lose the
        // reading entirely.
        if (maxAge is { } age && reading!.Value.ReportedAt is { } reportedAt)
        {
            var staleness = DateTimeOffset.UtcNow - reportedAt;
            if (staleness > age)
            {
                _logger.LogInformation(
                    "Ignoring SoC for {EntityId}: last reported {StalenessMinutes:F1} min ago, older than the {MaxAgeMinutes:F0} min limit.",
                    entityId, staleness.TotalMinutes, age.TotalMinutes);
                return null;
            }
        }

        return value;
    }

    public async Task<string?> GetStateAsync(string entityId, CancellationToken ct = default) =>
        (await GetEntityStateAsync(entityId, ct))?.State;

    /// <summary>One entity's state plus when the integration last reported it.</summary>
    private readonly record struct EntityStateReading(string? State, DateTimeOffset? ReportedAt);

    /// <summary>
    /// When Home Assistant last *reported* this state, which is not when it last changed: HA only
    /// bumps <c>last_changed</c>/<c>last_updated</c> when the value actually differs, so a steady
    /// SoC looks arbitrarily stale under those. <c>last_reported</c> moves on every poll but needs
    /// HA 2024.6+, hence the fallbacks. Public for the unit test.
    /// </summary>
    public static DateTimeOffset? ReadReportedAt(JsonElement root)
    {
        foreach (var property in new[] { "last_reported", "last_updated", "last_changed" })
        {
            if (root.TryGetProperty(property, out var element)
                && element.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(
                    element.GetString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                return timestamp;
            }
        }

        return null;
    }

    private async Task<EntityStateReading?> GetEntityStateAsync(string entityId, CancellationToken ct)
    {
        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            return null;
        }

        var accessToken = await _connectionManager.GetValidAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Concatenate rather than use BaseAddress + rooted path: the Supervisor Core proxy base
        // carries a path segment (http://supervisor/core) that a leading-slash path would drop.
        var baseUrl = connection.BaseUrl.TrimEnd('/');

        var response = await client.GetAsync($"{baseUrl}/api/states/{entityId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("state", out var stateElement))
        {
            return new EntityStateReading(stateElement.GetString(), ReadReportedAt(doc.RootElement));
        }

        return null;
    }

    public async Task CallServiceAsync(string domain, string service, string? dataJson, CancellationToken ct = default)
    {
        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            throw new InvalidOperationException("Not connected to Home Assistant");
        }

        var accessToken = await _connectionManager.GetValidAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Concatenate rather than use BaseAddress + rooted path: the Supervisor Core proxy base
        // carries a path segment (http://supervisor/core) that a leading-slash path would drop.
        var baseUrl = connection.BaseUrl.TrimEnd('/');

        var body = new StringContent(dataJson ?? "{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{baseUrl}/api/services/{domain}/{service}", body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<HaEntitySummary>> GetEntitiesAsync(CancellationToken ct = default)
    {
        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            return Array.Empty<HaEntitySummary>();
        }

        var accessToken = await _connectionManager.GetValidAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Concatenate rather than use BaseAddress + rooted path: the Supervisor Core proxy base
        // carries a path segment (http://supervisor/core) that a leading-slash path would drop.
        var baseUrl = connection.BaseUrl.TrimEnd('/');

        var response = await client.GetAsync($"{baseUrl}/api/states", ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var result = new List<HaEntitySummary>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var entityId = element.GetProperty("entity_id").GetString();
            if (entityId == null)
            {
                continue;
            }

            var state = element.TryGetProperty("state", out var stateElement) ? stateElement.GetString() : null;

            string? friendlyName = null;
            if (element.TryGetProperty("attributes", out var attributesElement) &&
                attributesElement.TryGetProperty("friendly_name", out var friendlyNameElement))
            {
                friendlyName = friendlyNameElement.GetString();
            }

            result.Add(new HaEntitySummary(entityId, friendlyName, state));
        }

        return result;
    }

    public async Task<IReadOnlyList<HaServiceDomain>> GetServicesAsync(CancellationToken ct = default)
    {
        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            return Array.Empty<HaServiceDomain>();
        }

        var accessToken = await _connectionManager.GetValidAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Concatenate rather than use BaseAddress + rooted path: the Supervisor Core proxy base
        // carries a path segment (http://supervisor/core) that a leading-slash path would drop.
        var baseUrl = connection.BaseUrl.TrimEnd('/');

        var response = await client.GetAsync($"{baseUrl}/api/services", ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var result = new List<HaServiceDomain>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var domain = element.TryGetProperty("domain", out var domainElement) ? domainElement.GetString() : null;
            if (domain == null)
            {
                continue;
            }

            var services = new List<string>();
            if (element.TryGetProperty("services", out var servicesElement))
            {
                foreach (var serviceProperty in servicesElement.EnumerateObject())
                {
                    services.Add(serviceProperty.Name);
                }
            }

            services.Sort(StringComparer.Ordinal);
            result.Add(new HaServiceDomain(domain, services));
        }

        result.Sort((a, b) => string.Compare(a.Domain, b.Domain, StringComparison.Ordinal));
        return result;
    }
}
