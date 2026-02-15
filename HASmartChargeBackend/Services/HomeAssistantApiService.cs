using HASmartChargeBackend.Models.HomeAssistant;
using HASmartChargeBackend.Services.Auth.Interfaces;
using HASmartChargeBackend.Services.Interfaces;

namespace HASmartChargeBackend.Services;

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
        client.BaseAddress = new Uri(connection.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AccessToken);
        
        var response = await client.GetAsync("/api/states");
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