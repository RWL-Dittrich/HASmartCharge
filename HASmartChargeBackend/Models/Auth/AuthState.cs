namespace HASmartChargeBackend.Models.Auth;

public class AuthState
{
    public required string State { get; set; }
    public required string BaseUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? AuthorizationCode { get; set; }
}

