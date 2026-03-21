using HASmartCharge.Backend.DB.Models;
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
        HomeAssistantConnection? connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            throw new Exception("Connection not found");
        }

        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(connection.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/api/states");
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        List<HaEntity>? entities = System.Text.Json.JsonSerializer.Deserialize<List<HaEntity>>(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        return entities;
    }
}
