using HASmartChargeBackend.Models.Auth;

namespace HASmartChargeBackend.Services.Auth.Interfaces;

public interface IHomeAssistantAuthService
{
    /// <summary>
    /// Generates the Home Assistant authorization URL and stores the state
    /// </summary>
    /// <param name="baseUrl">The Home Assistant base URL</param>
    /// <param name="redirectUri">The callback URL where Home Assistant will redirect after authorization</param>
    /// <param name="clientId">The OAuth client ID (typically the application's base URL)</param>
    string GenerateAuthorizationUrl(string baseUrl, string redirectUri, string clientId);
    
    /// <summary>
    /// Validates the callback state and stores the authorization code
    /// </summary>
    bool ValidateAndStoreAuthorizationCode(string state, string authorizationCode);
    
    /// <summary>
    /// Retrieves the authorization code for a given state
    /// </summary>
    string? GetAuthorizationCode(string state);
    
    /// <summary>
    /// Retrieves the auth state for a given state token
    /// </summary>
    AuthState? GetAuthState(string state);
}

