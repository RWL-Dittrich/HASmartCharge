using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using HASmartCharge.Backend.HomeAssistant.Models;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;

namespace HASmartCharge.Backend.HomeAssistant.Services;

public class HomeAssistantApiService : IHomeAssistantApiService
{
    private readonly IHomeAssistantConnectionManager _connectionManager;
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeAssistantApiService(
        IHomeAssistantConnectionManager connectionManager,
        IHttpClientFactory httpClientFactory)
    {
        _connectionManager = connectionManager;
        _httpClientFactory = httpClientFactory;
    }

    //Get devices
    public async Task<List<HaEntity>> GetDevicesAsync()
    {
        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            throw new Exception("Connection not found");
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AccessToken);
        // Concatenate rather than BaseAddress + rooted path so a base with a path segment
        // (Supervisor proxy: http://supervisor/core) isn't dropped.
        var baseUrl = connection.BaseUrl.TrimEnd('/');

        var response = await client.GetAsync($"{baseUrl}/api/states");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var entities = System.Text.Json.JsonSerializer.Deserialize<List<HaEntity>>(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        return entities;
    }
}
