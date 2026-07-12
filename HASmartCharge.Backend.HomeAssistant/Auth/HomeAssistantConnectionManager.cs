using System.Text.Json;
using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using HASmartCharge.Backend.HomeAssistant.Models;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.HomeAssistant.Auth;

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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Load the first (and should be only) connection from the database
            var connection = await dbContext.HomeAssistantConnections.FirstOrDefaultAsync();

            if (connection != null)
            {
                lock (_lock)
                {
                    _connection = connection;
                    _initialized = true;
                }

                //Try accessing the HA instance to verify the token is still valid
                var httpClient = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{connection.BaseUrl}/api/");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    connection.TokenType, connection.AccessToken);

                var response = await httpClient.SendAsync(request);
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    // A rejected access token at startup is expected whenever the backend
                    // was offline past the ~30 min token lifetime — the access token is
                    // short-lived, the refresh token is not. Try a refresh before giving
                    // up; only a definitively rejected refresh (invalid_grant) clears the
                    // stored connection (RefreshAccessTokenAsync does that itself).
                    _logger.LogInformation(
                        "Stored Home Assistant access token was rejected at startup ({StatusCode}); attempting a refresh.",
                        response.StatusCode);
                    try
                    {
                        await RefreshAccessTokenAsync();
                        _logger.LogInformation("Refreshed the Home Assistant access token at startup.");
                    }
                    catch (Exception refreshEx)
                    {
                        bool wiped;
                        lock (_lock)
                        {
                            wiped = _connection == null;
                        }

                        if (wiped)
                        {
                            _logger.LogWarning(refreshEx,
                                "Home Assistant refresh token was rejected at startup; cleared stored connection.");
                        }
                        else
                        {
                            _logger.LogWarning(refreshEx,
                                "Could not refresh the Home Assistant token at startup (transient failure); keeping stored connection for the refresh service to retry.");
                        }
                    }
                }
                else if (!response.IsSuccessStatusCode)
                {
                    // HA might be restarting/unreachable — keep the tokens and let the
                    // token-refresh service retry.
                    _logger.LogWarning("Could not verify Home Assistant connection during initialization (status {StatusCode}); keeping stored connection.", response.StatusCode);
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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Remove all existing connections (we only support one)
            var existing = await dbContext.HomeAssistantConnections.ToListAsync();
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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find and update the existing connection
            var existing = await dbContext.HomeAssistantConnections
                .FirstOrDefaultAsync(c => c.BaseUrl == connection.BaseUrl);

            if (existing != null)
            {
                existing.ClientId = connection.ClientId;
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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Remove all connections
            var existing = await dbContext.HomeAssistantConnections.ToListAsync();
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

        var httpClient = _httpClientFactory.CreateClient();
        baseUrl = baseUrl.TrimEnd('/');

        var tokenUrl = $"{baseUrl}/auth/token";

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", authorizationCode },
            { "client_id", clientId }
        };

        var content = new FormUrlEncodedContent(requestBody);

        try
        {
            var response = await httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);
                throw new Exception($"Failed to exchange authorization code: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent)
                ?? throw new Exception("Failed to deserialize token response");

            // The authorization_code grant must return a refresh token; without one we
            // could never refresh, so treat its absence as a hard failure.
            if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                throw new Exception("Token exchange response did not contain a refresh token");
            }

            var now = DateTime.UtcNow;
            var connection = new HomeAssistantConnection
            {
                BaseUrl = baseUrl,
                ClientId = clientId,
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

        var httpClient = _httpClientFactory.CreateClient();
        var tokenUrl = $"{connection.BaseUrl}/auth/token";

        // HA requires the client_id used during authorization (this app's URL).
        // Older rows predate the ClientId column; BaseUrl is a last-resort fallback.
        var clientId = string.IsNullOrWhiteSpace(connection.ClientId) ? connection.BaseUrl : connection.ClientId;

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", connection.RefreshToken },
            { "client_id", clientId }
        };

        var content = new FormUrlEncodedContent(requestBody);

        try
        {
            var response = await httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token refresh failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                // Only wipe the stored connection when HA definitively revoked the grant.
                // Transient failures (network, HA restarting, 5xx) keep the tokens so the
                // next refresh cycle can retry.
                if (errorContent.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Refresh token rejected as invalid_grant; clearing stored connection.");
                    await DisconnectAsync();
                }

                throw new Exception($"Failed to refresh token: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent)
                ?? throw new Exception("Failed to deserialize token response");

            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_connection != null)
                {
                    _connection.AccessToken = tokenResponse.AccessToken;
                    // HA's refresh_token grant does NOT return a refresh token — it keeps
                    // the same one. Only overwrite if HA ever starts returning a new one;
                    // otherwise preserve the stored token so future refreshes keep working.
                    if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
                    {
                        _connection.RefreshToken = tokenResponse.RefreshToken;
                    }
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
