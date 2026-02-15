using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Models.Auth;

namespace HASmartCharge.Backend.Services.Auth.Interfaces;

/// <summary>
/// Manages the single Home Assistant connection and token lifecycle
/// </summary>
public interface IHomeAssistantConnectionManager
{
    /// <summary>
    /// Initialize the connection manager by loading stored connection from database.
    /// This should be called once during application startup.
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Exchange an authorization code for access and refresh tokens
    /// </summary>
    Task<HomeAssistantConnection> ExchangeCodeForTokensAsync(string authorizationCode, string baseUrl, string clientId);
    
    /// <summary>
    /// Get the current Home Assistant connection
    /// </summary>
    HomeAssistantConnection? GetConnection();
    
    /// <summary>
    /// Check if there's an active connection
    /// </summary>
    bool IsConnected();
    
    /// <summary>
    /// Get a valid access token, refreshing if necessary
    /// </summary>
    Task<string> GetValidAccessTokenAsync();
    
    /// <summary>
    /// Refresh the access token using the refresh token
    /// </summary>
    Task RefreshAccessTokenAsync();
    
    /// <summary>
    /// Disconnect from Home Assistant (clear stored tokens)
    /// </summary>
    void Disconnect();
}

