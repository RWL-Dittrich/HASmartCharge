using HASmartChargeBackend.Services;
using HASmartChargeBackend.Services.Auth;
using HASmartChargeBackend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HASmartChargeBackend.Controllers;

[ApiController]
[Route("api/homeassistant/auth")]
public class HomeAssistantAuthController : ControllerBase
{
    private readonly IHomeAssistantAuthService _authService;
    private readonly IHomeAssistantConnectionManager _connectionManager;
    private readonly ILogger<HomeAssistantAuthController> _logger;

    public HomeAssistantAuthController(
        IHomeAssistantAuthService authService,
        IHomeAssistantConnectionManager connectionManager,
        ILogger<HomeAssistantAuthController> logger)
    {
        _authService = authService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the Home Assistant OAuth flow by redirecting to the Home Assistant authorization page
    /// </summary>
    /// <param name="baseUrl">The base URL of the Home Assistant instance (e.g., http://homeassistant.local:8123)</param>
    /// <returns>Redirect to Home Assistant authorization page</returns>
    [HttpGet("start")]
    public IActionResult StartAuth([FromQuery] string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("StartAuth called with empty baseUrl");
            return BadRequest(new { error = "baseUrl parameter is required" });
        }

        // Validate URL format
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("StartAuth called with invalid baseUrl: {BaseUrl}", baseUrl);
            return BadRequest(new { error = "Invalid baseUrl format" });
        }

        try
        {
            // Build both client ID and redirect URI from the request host
            var scheme = Request.Scheme; // http or https
            var host = Request.Host.Value; // includes port if present
            var clientId = $"{scheme}://{host}";
            var redirectUri = $"{scheme}://{host}/api/homeassistant/auth/callback";
            
            var authorizationUrl = _authService.GenerateAuthorizationUrl(baseUrl, redirectUri, clientId);
            _logger.LogInformation("Redirecting to Home Assistant with client ID: {ClientId} and redirect URI: {RedirectUri}", 
                clientId, redirectUri);
            
            return Redirect(authorizationUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating authorization URL");
            return StatusCode(500, new { error = "Failed to generate authorization URL" });
        }
    }

    /// <summary>
    /// Callback endpoint that receives the authorization code from Home Assistant
    /// </summary>
    /// <param name="code">The authorization code from Home Assistant</param>
    /// <param name="state">The state token for CSRF protection</param>
    /// <returns>Success response with connection details</returns>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            _logger.LogWarning("Callback called with missing state parameter");
            return BadRequest(new { error = "Missing state parameter" });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Callback called with missing code parameter");
            return BadRequest(new { error = "Missing authorization code" });
        }

        try
        {
            var success = _authService.ValidateAndStoreAuthorizationCode(state, code);
            
            if (!success)
            {
                _logger.LogWarning("Failed to validate state or store authorization code");
                return BadRequest(new { error = "Invalid or expired state token" });
            }

            // Get the auth state to retrieve the base URL
            var authState = _authService.GetAuthState(state);
            if (authState == null)
            {
                _logger.LogWarning("Auth state not found after validation");
                return BadRequest(new { error = "State not found" });
            }

            // Build client ID from request
            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var clientId = $"{scheme}://{host}";

            // Exchange code for tokens
            _logger.LogInformation("Exchanging authorization code for tokens");
            var connection = await _connectionManager.ExchangeCodeForTokensAsync(code, authState.BaseUrl, clientId);
            
            _logger.LogInformation("Successfully established connection to Home Assistant");
            
            return Ok(new
            {
                success = true,
                message = "Successfully connected to Home Assistant",
                connected_at = connection.ConnectedAt,
                expires_at = connection.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback");
            return StatusCode(500, new { error = "Failed to process authorization callback" });
        }
    }

    /// <summary>
    /// Retrieves the authorization code for a given state token
    /// </summary>
    /// <param name="state">The state token</param>
    /// <returns>Authorization code if available</returns>
    [HttpGet("code")]
    public IActionResult GetAuthorizationCode([FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { error = "State parameter is required" });
        }

        var code = _authService.GetAuthorizationCode(state);
        
        if (code == null)
        {
            return NotFound(new { error = "Authorization code not found or expired" });
        }

        return Ok(new
        {
            success = true,
            code,
            state
        });
    }
    
    /// <summary>
    /// Get the current Home Assistant connection status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetConnectionStatus()
    {
        if (!_connectionManager.IsConnected())
        {
            return Ok(new
            {
                connected = false,
                message = "Not connected to Home Assistant"
            });
        }

        var connection = _connectionManager.GetConnection();
        if (connection == null)
        {
            return Ok(new
            {
                connected = false,
                message = "Not connected to Home Assistant"
            });
        }

        return Ok(new
        {
            connected = true,
            base_url = connection.BaseUrl,
            connected_at = connection.ConnectedAt,
            expires_at = connection.ExpiresAt,
            last_refreshed_at = connection.LastRefreshedAt,
            time_until_expiry_minutes = (connection.ExpiresAt - DateTime.UtcNow).TotalMinutes
        });
    }
    
    /// <summary>
    /// Disconnect from Home Assistant
    /// </summary>
    [HttpPost("disconnect")]
    public IActionResult Disconnect()
    {
        _connectionManager.Disconnect();
        _logger.LogInformation("User requested disconnect from Home Assistant");
        
        return Ok(new
        {
            success = true,
            message = "Disconnected from Home Assistant"
        });
    }
}





