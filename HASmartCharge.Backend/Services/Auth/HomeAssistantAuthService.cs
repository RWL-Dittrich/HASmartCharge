using System.Security.Cryptography;
using HASmartCharge.Backend.Configuration;
using HASmartCharge.Backend.Models.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;
using Microsoft.Extensions.Options;

namespace HASmartCharge.Backend.Services.Auth;

public class HomeAssistantAuthService : IHomeAssistantAuthService
{
    private readonly IAuthStateStore _authStateStore;
    private readonly HomeAssistantAuthOptions _options;
    private readonly ILogger<HomeAssistantAuthService> _logger;

    public HomeAssistantAuthService(
        IAuthStateStore authStateStore,
        IOptions<HomeAssistantAuthOptions> options,
        ILogger<HomeAssistantAuthService> logger)
    {
        _authStateStore = authStateStore;
        _options = options.Value;
        _logger = logger;
    }

    public string GenerateAuthorizationUrl(string baseUrl, string redirectUri, string clientId)
    {
        // Normalize base URL (remove trailing slash)
        baseUrl = baseUrl.TrimEnd('/');
        
        // Generate cryptographically secure random state
        string state = GenerateSecureState();
        
        // Store the state
        AuthState authState = new AuthState
        {
            State = state,
            BaseUrl = baseUrl,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.StateExpirationMinutes)
        };
        
        _authStateStore.StoreState(authState);
        
        // Build the authorization URL
        string authUrl = $"{baseUrl}/auth/authorize?" +
                      $"client_id={Uri.EscapeDataString(clientId)}&" +
                      $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                      $"state={Uri.EscapeDataString(state)}";
        
        _logger.LogInformation("Generated authorization URL for base URL: {BaseUrl} with client ID: {ClientId} and redirect URI: {RedirectUri}", 
            baseUrl, clientId, redirectUri);
        
        return authUrl;
    }

    public bool ValidateAndStoreAuthorizationCode(string state, string authorizationCode)
    {
        AuthState? authState = _authStateStore.GetState(state);
        
        if (authState == null)
        {
            _logger.LogWarning("Failed to validate state - state not found or expired: {State}", state);
            return false;
        }
        
        bool success = _authStateStore.UpdateAuthorizationCode(state, authorizationCode);
        
        if (success)
        {
            _logger.LogInformation("Successfully stored authorization code for state: {State}", state);
        }
        
        return success;
    }

    public string? GetAuthorizationCode(string state)
    {
        AuthState? authState = _authStateStore.GetState(state);
        return authState?.AuthorizationCode;
    }
    
    public AuthState? GetAuthState(string state)
    {
        return _authStateStore.GetState(state);
    }

    private static string GenerateSecureState()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}

