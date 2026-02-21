using System.Net.WebSockets;
using HASmartCharge.Backend.OCPP.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// OCPP 1.6J WebSocket Controller
/// Handles WebSocket connections from EV charge points
/// </summary>
[ApiController]
[Route("/ocpp")]
public class OcppController : ControllerBase
{
    private readonly ILogger<OcppController> _logger;
    private readonly OcppConnectionOrchestrator _orchestrator;

    public OcppController(OcppConnectionOrchestrator orchestrator, ILogger<OcppController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// OCPP 1.6J WebSocket endpoint
    /// Accepts WebSocket connections from charge points at ws://host:port/ocpp/1.6/{chargePointId}
    /// </summary>
    /// <param name="chargePointId">Unique identifier for the charge point</param>
    [Route("1.6/{chargePointId}")]
    public async Task HandleWebSocket([FromRoute] string chargePointId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            _logger.LogInformation("Charge point connection request: {ChargePointId}", chargePointId);

            using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync("ocpp1.6");
            
            string remoteEndPoint = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            await _orchestrator.HandleConnectionAsync(webSocket, chargePointId, remoteEndPoint);
        }
        else
        {
            _logger.LogWarning("Non-WebSocket request received for: {ChargePointId}", chargePointId);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
        }
    }
}
