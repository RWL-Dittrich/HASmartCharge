using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.HomeAssistant.Models;

/// <summary>
/// Response from Home Assistant token endpoint
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }

    /// <summary>
    /// Only the <c>authorization_code</c> grant returns a refresh token. The
    /// <c>refresh_token</c> grant response omits this field (HA never rotates the
    /// refresh token), so it must be optional — marking it <c>required</c> makes
    /// System.Text.Json throw on every refresh response.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
