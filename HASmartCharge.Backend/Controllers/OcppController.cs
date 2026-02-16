using HASmartCharge.Backend.Services.Ocpp;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

[ApiController]
public class OcppController : ControllerBase
{
    private readonly ILogger<OcppController> _logger;
    private readonly IOcppServerFactory _ocppServerFactory;

    public OcppController(IOcppServerFactory ocppServerFactory, ILogger<OcppController> logger)
    {
        _ocppServerFactory = ocppServerFactory;
        _logger = logger;
    }

    /// <summary>
    /// OCPP 1.6J WebSocket endpoint for charge points to connect
    /// </summary>
    /// <param name="chargePointId">The unique identifier of the charge point</param>
    [Route("/ocpp16/{chargePointId}")]
    public async Task Ocpp16WebSocket([FromRoute] string chargePointId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            _logger.LogInformation("Charge point connecting: {chargePointId}", chargePointId);

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync("ocpp1.6");

            var ocppServer = _ocppServerFactory.CreateServer(chargePointId);
            ocppServer.Start(webSocket);

            // Keep the connection alive until the WebSocket is closed
            while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await Task.Delay(1000);
            }

            _logger.LogInformation("Charge point disconnected: {chargePointId}", chargePointId);
        }
        else
        {
            _logger.LogWarning("Non-WebSocket request received for charge point: {chargePointId}", chargePointId);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
