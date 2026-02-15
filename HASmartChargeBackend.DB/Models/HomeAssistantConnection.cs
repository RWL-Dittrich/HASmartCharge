using System.ComponentModel.DataAnnotations;

namespace HASmartChargeBackend.DB.Models;

/// <summary>
/// Represents the stored Home Assistant connection with tokens
/// </summary>
public class HomeAssistantConnection
{
    /// <summary>
    /// The base URL of the Home Assistant instance
    /// </summary>
    [Key]
    public required string BaseUrl { get; set; }
    
    /// <summary>
    /// The access token for API calls
    /// </summary>
    public required string AccessToken { get; set; }
    
    /// <summary>
    /// The refresh token to get new access tokens
    /// </summary>
    public required string RefreshToken { get; set; }
    
    /// <summary>
    /// The type of token (usually "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
    
    /// <summary>
    /// Number of seconds until the access token expires
    /// </summary>
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// When this connection was established
    /// </summary>
    public DateTime ConnectedAt { get; set; }
    
    /// <summary>
    /// When the token was last refreshed
    /// </summary>
    public DateTime LastRefreshedAt { get; set; }
}

