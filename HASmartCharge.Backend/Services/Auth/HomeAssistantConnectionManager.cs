using System.Text.Json;
using HASmartCharge.Backend.Models.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services.Auth;

public class HomeAssistantConnectionManager : IHomeAssistantConnectionManager
{
    private HomeAssistantConnection? _connection;
    private readonly object _lock = new();
    private readonly ILogger<HomeAssistantConnectionManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _initialized;

    public HomeAssistantConnectionManager(
        ILogger<HomeAssistantConnectionManager> logger,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
    }
    
    /// <summary>
    /// Initialize the connection manager by loading stored connection from database.
    /// This should be called once during application startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Load the first (and should be only) connection from the database
            HomeAssistantConnection? connection = await dbContext.HomeAssistantConnections.FirstOrDefaultAsync();
            
            if (connection != null)
            {
                lock (_lock)
                {
                    _connection = connection;
                    _initialized = true;
                }
                
                //Try accessing the HA instance to verify the token is still valid
                HttpClient httpClient = _httpClientFactory.CreateClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{connection.BaseUrl}/api/");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    connection.TokenType, connection.AccessToken);
                
                HttpResponseMessage response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to verify Home Assistant connection during initialization. Status: {StatusCode}. Clearing stored connection.", response.StatusCode);
                    await ClearConnectionAsync();
                    lock (_lock)
                    {
                        _connection = null;
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Loaded existing Home Assistant connection from database. Connected at {ConnectedAt}, expires at {ExpiresAt}",
                        connection.ConnectedAt, connection.ExpiresAt);
                }
            }
            else
            {
                lock (_lock)
                {
                    _initialized = true;
                }
                
                _logger.LogInformation("No existing Home Assistant connection found in database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Home Assistant connection from database");
            lock (_lock)
            {
                _initialized = true;
            }
        }
    }
    
    private async Task EnsureInitializedAsync()
    {
        // Wait for initialization to complete
        while (!_initialized)
        {
            await Task.Delay(100);
        }
    }
    
    private async Task SaveConnectionAsync(HomeAssistantConnection connection)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Remove all existing connections (we only support one)
            List<HomeAssistantConnection> existing = await dbContext.HomeAssistantConnections.ToListAsync();
            if (existing.Any())
            {
                dbContext.HomeAssistantConnections.RemoveRange(existing);
            }
            
            // Add the new connection
            dbContext.HomeAssistantConnections.Add(connection);
            await dbContext.SaveChangesAsync();
            
            _logger.LogDebug("Saved Home Assistant connection to database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Home Assistant connection to database");
        }
    }
    
    private async Task UpdateConnectionAsync(HomeAssistantConnection connection)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Find and update the existing connection
            HomeAssistantConnection? existing = await dbContext.HomeAssistantConnections
                .FirstOrDefaultAsync(c => c.BaseUrl == connection.BaseUrl);
            
            if (existing != null)
            {
                existing.AccessToken = connection.AccessToken;
                existing.RefreshToken = connection.RefreshToken;
                existing.TokenType = connection.TokenType;
                existing.ExpiresIn = connection.ExpiresIn;
                existing.ExpiresAt = connection.ExpiresAt;
                existing.LastRefreshedAt = connection.LastRefreshedAt;
                
                await dbContext.SaveChangesAsync();
                _logger.LogDebug("Updated Home Assistant connection in database");
            }
            else
            {
                // If not found, save as new
                await SaveConnectionAsync(connection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Home Assistant connection in database");
        }
    }
    
    private async Task ClearConnectionAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Remove all connections
            List<HomeAssistantConnection> existing = await dbContext.HomeAssistantConnections.ToListAsync();
            if (existing.Any())
            {
                dbContext.HomeAssistantConnections.RemoveRange(existing);
                await dbContext.SaveChangesAsync();
                _logger.LogDebug("Cleared Home Assistant connection from database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Home Assistant connection from database");
        }
    }

    public async Task<HomeAssistantConnection> ExchangeCodeForTokensAsync(
        string authorizationCode, 
        string baseUrl, 
        string clientId)
    {
        await EnsureInitializedAsync();
        
        _logger.LogInformation("Exchanging authorization code for tokens with Home Assistant at {BaseUrl}", baseUrl);
        
        HttpClient httpClient = _httpClientFactory.CreateClient();
        baseUrl = baseUrl.TrimEnd('/');
        
        string tokenUrl = $"{baseUrl}/auth/token";
        
        Dictionary<string, string> requestBody = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", authorizationCode },
            { "client_id", clientId }
        };
        
        FormUrlEncodedContent content = new FormUrlEncodedContent(requestBody);
        
        try
        {
            HttpResponseMessage response = await httpClient.PostAsync(tokenUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Failed to exchange authorization code: {response.StatusCode}");
            }
            
            string responseContent = await response.Content.ReadAsStringAsync();
            TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent)
                ?? throw new Exception("Failed to deserialize token response");
            
            DateTime now = DateTime.UtcNow;
            HomeAssistantConnection connection = new HomeAssistantConnection
            {
                BaseUrl = baseUrl,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = tokenResponse.TokenType,
                ExpiresIn = tokenResponse.ExpiresIn,
                ExpiresAt = now.AddSeconds(tokenResponse.ExpiresIn),
                ConnectedAt = now,
                LastRefreshedAt = now
            };
            
            lock (_lock)
            {
                _connection = connection;
            }
            
            // Save to database
            await SaveConnectionAsync(connection);
            
            _logger.LogInformation("Successfully connected to Home Assistant. Token expires at {ExpiresAt}", 
                connection.ExpiresAt);
            
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for tokens");
            throw;
        }
    }

    public HomeAssistantConnection? GetConnection()
    {
        // Ensure initialization is complete (synchronous wait for async operation)
        EnsureInitializedAsync().GetAwaiter().GetResult();
        
        lock (_lock)
        {
            return _connection;
        }
    }

    public bool IsConnected()
    {
        // Ensure initialization is complete (synchronous wait for async operation)
        EnsureInitializedAsync().GetAwaiter().GetResult();
        
        lock (_lock)
        {
            return _connection != null;
        }
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        HomeAssistantConnection? connection;
        lock (_lock)
        {
            connection = _connection;
        }
        
        if (connection == null)
        {
            throw new InvalidOperationException("Not connected to Home Assistant");
        }
        
        // Refresh token if it expires in less than 5 minutes
        if (connection.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Access token expiring soon, refreshing...");
            await RefreshAccessTokenAsync();
            
            lock (_lock)
            {
                connection = _connection;
            }
            
            if (connection == null)
            {
                throw new InvalidOperationException("Connection lost after refresh");
            }
        }
        
        return connection.AccessToken;
    }

    public async Task RefreshAccessTokenAsync()
    {
        HomeAssistantConnection? connection;
        lock (_lock)
        {
            connection = _connection;
        }
        
        if (connection == null)
        {
            _logger.LogWarning("Cannot refresh token - not connected");
            return;
        }
        
        _logger.LogInformation("Refreshing access token for Home Assistant at {BaseUrl}", connection.BaseUrl);
        
        HttpClient httpClient = _httpClientFactory.CreateClient();
        string tokenUrl = $"{connection.BaseUrl}/auth/token";
        
        Dictionary<string, string> requestBody = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", connection.RefreshToken },
            { "client_id", connection.BaseUrl } // Client ID should be the base URL
        };
        
        FormUrlEncodedContent content = new FormUrlEncodedContent(requestBody);
        
        try
        {
            HttpResponseMessage response = await httpClient.PostAsync(tokenUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token refresh failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                
                // If refresh fails, disconnect
                await DisconnectAsync();
                throw new Exception($"Failed to refresh token: {response.StatusCode}");
            }
            
            string responseContent = await response.Content.ReadAsStringAsync();
            TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent)
                ?? throw new Exception("Failed to deserialize token response");
            
            DateTime now = DateTime.UtcNow;
            
            lock (_lock)
            {
                if (_connection != null)
                {
                    _connection.AccessToken = tokenResponse.AccessToken;
                    _connection.RefreshToken = tokenResponse.RefreshToken;
                    _connection.TokenType = tokenResponse.TokenType;
                    _connection.ExpiresIn = tokenResponse.ExpiresIn;
                    _connection.ExpiresAt = now.AddSeconds(tokenResponse.ExpiresIn);
                    _connection.LastRefreshedAt = now;
                    
                    connection = _connection;
                }
            }
            
            // Update in database
            if (connection != null)
            {
                await UpdateConnectionAsync(connection);
            }
            
            _logger.LogInformation("Successfully refreshed access token. New token expires at {ExpiresAt}", 
                now.AddSeconds(tokenResponse.ExpiresIn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            throw;
        }
    }

    public void Disconnect()
    {
        // Call async version synchronously
        DisconnectAsync().GetAwaiter().GetResult();
    }
    
    private async Task DisconnectAsync()
    {
        lock (_lock)
        {
            _connection = null;
        }
        
        // Clear from database
        await ClearConnectionAsync();
        
        _logger.LogInformation("Disconnected from Home Assistant");
    }
}


