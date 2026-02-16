using HASmartCharge.Backend.OCPP.Services;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// OCPP 1.6J WebSocket Controller
/// Handles WebSocket connections from EV charge points
/// </summary>
[ApiController]
public class OcppController : ControllerBase
{
    private readonly ILogger<OcppController> _logger;
    private readonly OcppServerService _ocppServer;

    public OcppController(OcppServerService ocppServer, ILogger<OcppController> logger)
    {
        _ocppServer = ocppServer;
        _logger = logger;
    }

    /// <summary>
    /// OCPP 1.6J WebSocket endpoint
    /// Accepts WebSocket connections from charge points at ws://host:port/ocpp16/{chargePointId}
    /// </summary>
    /// <param name="chargePointId">Unique identifier for the charge point</param>
    [Route("/ocpp16/{chargePointId}")]
    public async Task HandleWebSocket([FromRoute] string chargePointId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            _logger.LogInformation("Charge point connection request: {ChargePointId}", chargePointId);

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync("ocpp1.6");
            
            await _ocppServer.HandleConnection(webSocket, chargePointId);
        }
        else
        {
            _logger.LogWarning("Non-WebSocket request received for: {ChargePointId}", chargePointId);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
        }
    }
}
