using HASmartCharge.Backend.Models.Charger;
using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Models;
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
    private readonly ISessionManager _sessionManager;

    public ChargerCommandsController(
        ILogger<ChargerCommandsController> logger,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    // -------------------------------------------------------------------------
    // Charger-level commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reset the charger.
    /// </summary>
    /// <param name="chargerId">Charge point ID</param>
    /// <param name="request">Reset type — <c>Hard</c> or <c>Soft</c> (default: Soft)</param>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reset(
        [FromRoute] string chargerId,
        [FromBody] ResetRequest request)
    {
        if (request.Type != "Hard" && request.Type != "Soft")
            return BadRequest(new { error = "Type must be 'Hard' or 'Soft'" });

        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation("Sending Reset ({Type}) to {ChargerId}", request.Type, chargerId);

        OcppCommandResult result = await session.SendCommandAsync("Reset", new ResetRequest { Type = request.Type });
        return OcppResultToActionResult(result, new { chargerId, type = request.Type });
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
        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation("Sending ClearCache to {ChargerId}", chargerId);

        OcppCommandResult result = await session.SendCommandAsync("ClearCache", new { });
        return OcppResultToActionResult(result, new { chargerId });
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
        [FromBody] TriggerMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedMessage))
            return BadRequest(new { error = "requestedMessage is required" });

        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation(
            "Sending TriggerMessage ({RequestedMessage}, connector {ConnectorId}) to {ChargerId}",
            request.RequestedMessage, request.ConnectorId, chargerId);

        OcppCommandResult result = await session.SendCommandAsync("TriggerMessage", new TriggerMessageRequest
        {
            RequestedMessage = request.RequestedMessage,
            ConnectorId      = request.ConnectorId
        });

        return OcppResultToActionResult(result, new { chargerId, requestedMessage = request.RequestedMessage, connectorId = request.ConnectorId });
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
        [FromBody] GetDiagnosticsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Location))
            return BadRequest(new { error = "location is required" });

        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation("Sending GetDiagnostics to {ChargerId}, location={Location}", chargerId, request.Location);

        OcppCommandResult result = await session.SendCommandAsync("GetDiagnostics", new GetDiagnosticsRequest
        {
            Location      = request.Location,
            Retries       = request.Retries,
            RetryInterval = request.RetryInterval,
            StartTime     = request.StartTime,
            StopTime      = request.StopTime
        });

        return OcppResultToActionResult(result, new { chargerId, location = request.Location });
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

        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation(
            "Setting availability of {ChargerId} connector {ConnectorId} to {Type}",
            chargerId, connectorId, request.Type);

        bool available = request.Type == "Operative";
        OcppCommandResult result = await session.SetAvailabilityAsync(connectorId, available);

        return OcppResultToActionResult(result, new { chargerId, connectorId, type = request.Type });
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
        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation("Sending UnlockConnector to {ChargerId} connector {ConnectorId}", chargerId, connectorId);

        OcppCommandResult result = await session.SendCommandAsync("UnlockConnector", new UnlockConnectorRequest
        {
            ConnectorId = connectorId
        });

        return OcppResultToActionResult(result, new { chargerId, connectorId });
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
        [FromBody] RemoteStartTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdTag))
            return BadRequest(new { error = "idTag is required" });

        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation(
            "Sending RemoteStartTransaction to {ChargerId} connector {ConnectorId}, idTag={IdTag}",
            chargerId, connectorId, request.IdTag);

        OcppCommandResult result = await session.RemoteStartTransactionAsync(connectorId, request.IdTag);

        return OcppResultToActionResult(result, new { chargerId, connectorId, idTag = request.IdTag });
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
        IChargePointSession? session = GetSession(chargerId, out IActionResult? error);
        if (session == null) return error!;

        _logger.LogInformation(
            "Sending RemoteStopTransaction to {ChargerId} connector {ConnectorId}, transactionId={TransactionId}",
            chargerId, connectorId, transactionId);

        OcppCommandResult result = await session.RemoteStopTransactionAsync(transactionId);

        return OcppResultToActionResult(result, new { chargerId, connectorId, transactionId });
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the session for <paramref name="chargerId"/>.
    /// Returns <c>null</c> and sets <paramref name="error"/> to a 404 or 503
    /// result when the session cannot be resolved.
    /// </summary>
    private IChargePointSession? GetSession(string chargerId, out IActionResult? error)
    {
        IChargePointSession? session = _sessionManager.GetByChargePointId(chargerId);

        if (session == null)
        {
            // The session manager only tracks currently-connected sessions,
            // so null always means "not connected right now".
            error = _sessionManager.IsConnected(chargerId)
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Charger is not connected", chargerId })
                : NotFound(new { error = "Charger not connected", chargerId });
            return null;
        }

        error = null;
        return session;
    }

    /// <summary>
    /// Maps an <see cref="OcppCommandResult"/> to an <see cref="IActionResult"/>.
    /// 200 OK on success, 502 Bad Gateway on charger error or timeout.
    /// </summary>
    private IActionResult OcppResultToActionResult(OcppCommandResult result, object context)
    {
        if (result.Success)
        {
            return Ok(new { success = true, response = result.RawPayload, context });
        }

        return StatusCode(StatusCodes.Status502BadGateway, new
        {
            success = false,
            errorCode = result.ErrorCode,
            errorDescription = result.ErrorDescription,
            context
        });
    }
}



