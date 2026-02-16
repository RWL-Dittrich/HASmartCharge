using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.Models.Auth;

/// <summary>
/// Response from Home Assistant token endpoint
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

