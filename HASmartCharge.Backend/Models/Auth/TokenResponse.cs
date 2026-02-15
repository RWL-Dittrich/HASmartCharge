namespace HASmartCharge.Backend.Models.Auth;

/// <summary>
/// Response from Home Assistant token endpoint
/// </summary>
public class TokenResponse
{
    public required string access_token { get; set; }
    public required string token_type { get; set; }
    public required string refresh_token { get; set; }
    public int expires_in { get; set; }
}

