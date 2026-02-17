using HASmartCharge.Backend.Models.Charger;
using HASmartCharge.Backend.OCPP.Domain;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Controller for managing chargers and sending OCPP commands
/// </summary>
[ApiController]
[Route("api/charger")]
public class ChargerController : ControllerBase
{
    private readonly ILogger<ChargerController> _logger;
    private readonly ISessionManager _sessionManager;

    public ChargerController(ILogger<ChargerController> logger, ISessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Get list of connected chargers
    /// </summary>
    [HttpGet("connected")]
    public IActionResult GetConnectedChargers()
    {
        List<string> connectedChargers = _sessionManager.GetConnectedChargePointIds().ToList();
        return Ok(new { chargers = connectedChargers, count = connectedChargers.Count });
    }

    /// <summary>
    /// Check if a specific charger is connected
    /// </summary>
    [HttpGet("{chargerId}/status")]
    public IActionResult GetChargerStatus([FromRoute] string chargerId)
    {
        bool isConnected = _sessionManager.IsConnected(chargerId);
        return Ok(new { chargerId, isConnected });
    }

    /// <summary>
    /// Set the availability of a charger connector
    /// </summary>
    /// <param name="chargerId">The charge point ID</param>
    /// <param name="request">Availability request</param>
    [HttpPost("{chargerId}/availability")]
    public async Task<IActionResult> SetAvailability(
        [FromRoute] string chargerId, 
        [FromBody] SetAvailabilityRequest request)
    {
        IChargePointSession? session = _sessionManager.GetByChargePointId(chargerId);
        
        if (session == null)
        {
            _logger.LogWarning("Attempted to set availability for disconnected charger: {ChargerId}", chargerId);
            return NotFound(new { error = "Charger not connected", chargerId });
        }

        if (string.IsNullOrEmpty(request.Type) || 
            (request.Type != "Operative" && request.Type != "Inoperative"))
        {
            return BadRequest(new { error = "Type must be 'Operative' or 'Inoperative'" });
        }

        _logger.LogInformation(
            "Setting availability for charger {ChargerId}, connector {ConnectorId} to {Type}", 
            chargerId, request.ConnectorId, request.Type);

        bool available = request.Type == "Operative";
        bool success = await session.SetAvailabilityAsync(request.ConnectorId, available);

        if (success)
        {
            return Ok(new 
            { 
                success = true, 
                message = "ChangeAvailability command sent successfully",
                chargerId,
                connectorId = request.ConnectorId,
                type = request.Type
            });
        }
        else
        {
            return StatusCode(500, new { error = "Failed to send command to charger" });
        }
    }
}

