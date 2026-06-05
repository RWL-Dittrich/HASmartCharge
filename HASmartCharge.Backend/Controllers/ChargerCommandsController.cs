using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.Models.Charger;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// OCPP command endpoints — all operations that send a message to a charger.
/// Shares the same route prefix as <see cref="ChargersController"/>.
///
/// All command endpoints return 404 if the charger has never been seen,
/// or 503 if it is known but not currently connected.
/// A 200 response means the command was successfully dispatched to the charger
/// and the charger responded with a CALLRESULT.
/// A 502 response means the charger responded with a CALLERROR or timed out.
/// </summary>
[ApiController]
[Route("api/chargers/{chargerId}")]
[Produces("application/json")]
public class ChargerCommandsController : ControllerBase
{
    private readonly ILogger<ChargerCommandsController> _logger;
    private readonly IChargerGateway _chargerGateway;

    public ChargerCommandsController(
        ILogger<ChargerCommandsController> logger,
        IChargerGateway chargerGateway)
    {
        _logger = logger;
        _chargerGateway = chargerGateway;
    }

    // -------------------------------------------------------------------------
    // Charger-level commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reset the charger.
    /// </summary>
    /// <param name="chargerId">Charge point ID</param>
    /// <param name="request">Reset type — <c>Hard</c> or <c>Soft</c> (default: Hard)</param>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reset(
        [FromRoute] string chargerId,
        [FromBody] ResetChargerRequest request)
    {
        if (request.Type != "Hard" && request.Type != "Soft")
            return BadRequest(new { error = "Type must be 'Hard' or 'Soft'" });

        _logger.LogInformation("Sending Reset ({Type}) to {ChargerId}", request.Type, chargerId);

        var hardReset = request.Type == "Hard";
        var result = await _chargerGateway.ResetChargerAsync(chargerId, hardReset);
        return GatewayResultToActionResult(result, new { chargerId, type = request.Type });
    }

    /// <summary>
    /// Clear the charger's local authorisation cache.
    /// </summary>
    [HttpPost("clear-cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClearCache([FromRoute] string chargerId)
    {
        _logger.LogInformation("Sending ClearCache to {ChargerId}", chargerId);

        var result = await _chargerGateway.ClearCacheAsync(chargerId);
        return GatewayResultToActionResult(result, new { chargerId });
    }

    /// <summary>
    /// Ask the charger to send a specific OCPP message back to the CSMS.
    /// Valid values for <c>requestedMessage</c>: BootNotification,
    /// DiagnosticsStatusNotification, FirmwareStatusNotification,
    /// Heartbeat, MeterValues, StatusNotification.
    /// </summary>
    [HttpPost("trigger-message")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerMessage(
        [FromRoute] string chargerId,
        [FromBody] TriggerMessageBodyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedMessage))
            return BadRequest(new { error = "requestedMessage is required" });

        _logger.LogInformation(
            "Sending TriggerMessage ({RequestedMessage}, connector {ConnectorId}) to {ChargerId}",
            request.RequestedMessage, request.ConnectorId, chargerId);

        var result = await _chargerGateway.TriggerMessageAsync(chargerId, request.RequestedMessage, request.ConnectorId);
        return GatewayResultToActionResult(result, new { chargerId, requestedMessage = request.RequestedMessage, connectorId = request.ConnectorId });
    }

    /// <summary>
    /// Request the charger to upload a diagnostics file to the given location URL.
    /// </summary>
    [HttpPost("diagnostics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDiagnostics(
        [FromRoute] string chargerId,
        [FromBody] GetDiagnosticsBodyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Location))
            return BadRequest(new { error = "location is required" });

        _logger.LogInformation("Sending GetDiagnostics to {ChargerId}, location={Location}", chargerId, request.Location);

        var result = await _chargerGateway.GetDiagnosticsAsync(chargerId, request.Location);
        return GatewayResultToActionResult(result, new { chargerId, location = request.Location });
    }

    // -------------------------------------------------------------------------
    // Connector-level commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Set the availability of a specific connector (or all connectors when connectorId = 0).
    /// </summary>
    [HttpPut("connectors/{connectorId:int}/availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetAvailability(
        [FromRoute] string chargerId,
        [FromRoute] int connectorId,
        [FromBody] SetAvailabilityRequest request)
    {
        if (request.Type != "Operative" && request.Type != "Inoperative")
            return BadRequest(new { error = "Type must be 'Operative' or 'Inoperative'" });

        _logger.LogInformation(
            "Setting availability of {ChargerId} connector {ConnectorId} to {Type}",
            chargerId, connectorId, request.Type);

        var available = request.Type == "Operative";
        var result = await _chargerGateway.SetConnectorAvailabilityAsync(chargerId, connectorId, available);
        return GatewayResultToActionResult(result, new { chargerId, connectorId, type = request.Type });
    }

    /// <summary>
    /// Unlock a connector (physically release the cable lock).
    /// </summary>
    [HttpPost("connectors/{connectorId:int}/unlock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UnlockConnector(
        [FromRoute] string chargerId,
        [FromRoute] int connectorId)
    {
        _logger.LogInformation("Sending UnlockConnector to {ChargerId} connector {ConnectorId}", chargerId, connectorId);

        var result = await _chargerGateway.UnlockConnectorAsync(chargerId, connectorId);
        return GatewayResultToActionResult(result, new { chargerId, connectorId });
    }

    /// <summary>
    /// Remotely start a transaction on a connector.
    /// </summary>
    [HttpPost("connectors/{connectorId:int}/transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartTransaction(
        [FromRoute] string chargerId,
        [FromRoute] int connectorId,
        [FromBody] RemoteStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdTag))
            return BadRequest(new { error = "idTag is required" });

        _logger.LogInformation(
            "Sending RemoteStartTransaction to {ChargerId} connector {ConnectorId}, idTag={IdTag}",
            chargerId, connectorId, request.IdTag);

        var result = await _chargerGateway.StartTransactionAsync(chargerId, connectorId, request.IdTag);
        return GatewayResultToActionResult(result, new { chargerId, connectorId, idTag = request.IdTag });
    }

    /// <summary>
    /// Remotely stop a transaction by transaction ID.
    /// </summary>
    [HttpDelete("connectors/{connectorId:int}/transactions/{transactionId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StopTransaction(
        [FromRoute] string chargerId,
        [FromRoute] int connectorId,
        [FromRoute] int transactionId)
    {
        _logger.LogInformation(
            "Sending RemoteStopTransaction to {ChargerId} connector {ConnectorId}, transactionId={TransactionId}",
            chargerId, connectorId, transactionId);

        var result = await _chargerGateway.StopTransactionAsync(chargerId, transactionId);
        return GatewayResultToActionResult(result, new { chargerId, connectorId, transactionId });
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a <see cref="ChargerCommandResult"/> to an <see cref="IActionResult"/>.
    /// </summary>
    private IActionResult GatewayResultToActionResult(ChargerCommandResult result, object context)
    {
        if (result.Success)
            return Ok(new { success = true, response = result.RawPayload, context });

        return result.ErrorCode switch
        {
            "ChargerNotFound" => NotFound(new { error = "Charger not connected", context }),
            "ChargerOffline"  => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Charger is not connected", context }),
            _                 => StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                errorCode = result.ErrorCode,
                errorDescription = result.ErrorDescription,
                context
            })
        };
    }
}
