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
        // Log every hit to the endpoint before doing anything else — this is the earliest
        // point we can trace a (re)connect attempt, including ones that fail the WebSocket
        // handshake or aren't WS requests at all, so a charger stuck in a reconnect loop
        // still leaves a footprint here.
        var remoteEndPoint = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogInformation(
            "[{ChargePointId}] Incoming connection from {RemoteEndPoint} (WebSocket handshake: {IsWebSocket}, subprotocols: [{SubProtocols}])",
            chargePointId,
            remoteEndPoint,
            HttpContext.WebSockets.IsWebSocketRequest,
            string.Join(", ", HttpContext.WebSockets.WebSocketRequestedProtocols));

        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            // Keep sending WebSocket keepalive frames every 60s so NATs/proxies don't drop
            // the idle link. We deliberately do NOT set KeepAliveTimeout: that would put the
            // socket into ping-and-expect-pong mode and force-abort the connection when no
            // pong arrives — but many OCPP 1.6J chargers never answer WS-level ping frames
            // (they keep the link alive with OCPP Heartbeat messages instead), so the timeout
            // kills perfectly healthy chargers. Dead-link detection lives at the OCPP layer
            // instead: OcppConnectionOrchestrator aborts a session that sends no OCPP traffic
            // (Heartbeat is guaranteed every HeartbeatInterval) within its idle window.
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
            {
                SubProtocol = "ocpp1.6",
                KeepAliveInterval = TimeSpan.FromSeconds(60),
            });

            _logger.LogInformation(
                "[{ChargePointId}] WebSocket handshake accepted (subprotocol: {SubProtocol}) from {RemoteEndPoint}",
                chargePointId,
                webSocket.SubProtocol ?? "none",
                remoteEndPoint);

            await _orchestrator.HandleConnectionAsync(webSocket, chargePointId, remoteEndPoint);
        }
        else
        {
            _logger.LogWarning(
                "[{ChargePointId}] Non-WebSocket request from {RemoteEndPoint} rejected",
                chargePointId,
                remoteEndPoint);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
        }
    }
}
