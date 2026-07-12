using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

[ApiController]
[Route("api/ha")]
public class HomeAssistantController : ControllerBase
{
    private readonly IHomeAssistantConnectionManager _connectionManager;
    private readonly IHomeAssistantControl _control;

    public HomeAssistantController(
        IHomeAssistantConnectionManager connectionManager,
        IHomeAssistantControl control)
    {
        _connectionManager = connectionManager;
        _control = control;
    }

    /// <summary>
    /// Gets the current Home Assistant connection status.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var connection = _connectionManager.GetConnection();

        return Ok(new
        {
            connected = connection != null,
            baseUrl = connection?.BaseUrl,
            tokenExpiresAt = connection?.ExpiresAt
        });
    }

    /// <summary>
    /// Gets all Home Assistant entities, optionally filtered by domain (e.g. "sensor").
    /// </summary>
    [HttpGet("entities")]
    public async Task<IActionResult> GetEntities([FromQuery] string? domain, CancellationToken ct)
    {
        var entities = await _control.GetEntitiesAsync(ct);

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var prefix = domain.EndsWith('.') ? domain : $"{domain}.";
            entities = entities.Where(e => e.EntityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(entities);
    }

    /// <summary>
    /// Gets all Home Assistant service domains and their services, for use in the settings service autofill.
    /// </summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServices(CancellationToken ct)
    {
        var services = await _control.GetServicesAsync(ct);
        return Ok(services);
    }
}
