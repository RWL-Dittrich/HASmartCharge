using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Read-only charger status and telemetry endpoints.
/// All data is sourced from the in-memory <see cref="ChargerStatusTracker"/>.
/// </summary>
[ApiController]
[Route("api/chargers")]
[Produces("application/json")]
public class ChargersController : ControllerBase
{
    private readonly ChargerStatusTracker _statusTracker;

    public ChargersController(ChargerStatusTracker statusTracker)
    {
        _statusTracker = statusTracker;
    }

    // -------------------------------------------------------------------------
    // Charger collection
    // -------------------------------------------------------------------------

    /// <summary>
    /// List all known chargers.
    /// Pass <c>?connected=true</c> to return only currently-connected chargers.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetChargers([FromQuery] bool? connected = null)
    {
        IEnumerable<ChargerStatus> statuses = connected switch
        {
            true  => _statusTracker.GetConnectedChargers(),
            false => _statusTracker.GetAllChargerStatuses().Where(s => !s.IsConnected),
            null  => _statusTracker.GetAllChargerStatuses()
        };

        List<ChargerStatus> list = statuses.ToList();

        return Ok(new
        {
            count = list.Count,
            chargers = list.Select(MapChargerSummary)
        });
    }

    // -------------------------------------------------------------------------
    // Single charger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Get full status detail for a single charger including hardware info,
    /// all connector statuses and latest measurands.
    /// </summary>
    [HttpGet("{chargerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetCharger([FromRoute] string chargerId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        return Ok(MapChargerDetail(status));
    }

    // -------------------------------------------------------------------------
    // Connectors
    // -------------------------------------------------------------------------

    /// <summary>
    /// List all connectors for a charger with their status and latest measurands combined.
    /// </summary>
    [HttpGet("{chargerId}/connectors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetConnectors([FromRoute] string chargerId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        List<object> connectors = status.Connectors.Values
            .OrderBy(c => c.ConnectorId)
            .Select(c => MapConnectorDetail(status, c.ConnectorId))
            .ToList();

        return Ok(new { chargerId, count = connectors.Count, connectors });
    }

    /// <summary>
    /// Get status and latest measurands for a single connector.
    /// </summary>
    [HttpGet("{chargerId}/connectors/{connectorId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetConnector([FromRoute] string chargerId, [FromRoute] int connectorId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        if (!status.Connectors.ContainsKey(connectorId))
            return NotFound(new { error = "Connector not found", chargerId, connectorId });

        return Ok(MapConnectorDetail(status, connectorId));
    }

    // -------------------------------------------------------------------------
    // Transactions
    // -------------------------------------------------------------------------

    /// <summary>
    /// List all connectors that currently have an active transaction on this charger.
    /// </summary>
    [HttpGet("{chargerId}/transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetActiveTransactions([FromRoute] string chargerId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        List<object> transactions = status.Connectors.Values
            .Where(c => c.ActiveTransactionId.HasValue)
            .OrderBy(c => c.ConnectorId)
            .Select(c => (object)new
            {
                connectorId          = c.ConnectorId,
                transactionId        = c.ActiveTransactionId!.Value,
                idTag                = c.IdTag,
                startTime            = c.TransactionStartTime,
                connectorStatus      = c.Status,
                energyActiveImportWh = _statusTracker
                    .GetConnectorMeasurands(chargerId, c.ConnectorId)
                    ?.EnergyActiveImportRegister
            })
            .ToList();

        return Ok(new { chargerId, count = transactions.Count, transactions });
    }

    // -------------------------------------------------------------------------
    // Mapping helpers
    // -------------------------------------------------------------------------

    private static object MapChargerSummary(ChargerStatus s) => new
    {
        chargePointId   = s.ChargePointId,
        isConnected     = s.IsConnected,
        connectedAt     = s.ConnectedAt,
        disconnectedAt  = s.DisconnectedAt,
        lastUpdated     = s.LastUpdated,
        vendor          = s.Info?.Vendor,
        model           = s.Info?.Model,
        firmwareVersion = s.Info?.FirmwareVersion,
        connectorCount  = s.Connectors.Count
    };

    private static object MapChargerDetail(ChargerStatus s) => new
    {
        chargePointId  = s.ChargePointId,
        isConnected    = s.IsConnected,
        connectedAt    = s.ConnectedAt,
        disconnectedAt = s.DisconnectedAt,
        lastUpdated    = s.LastUpdated,
        info           = s.Info,
        connectors     = s.Connectors.Values
            .OrderBy(c => c.ConnectorId)
            .Select(c => MapConnectorDetail(s, c.ConnectorId))
    };

    private static object MapConnectorDetail(ChargerStatus s, int connectorId)
    {
        s.Connectors.TryGetValue(connectorId, out ConnectorStatus? connStatus);
        s.Measurands.TryGetValue(connectorId, out ConnectorMeasurands? measurands);

        return new
        {
            connectorId,
            status             = connStatus?.Status,
            errorCode          = connStatus?.ErrorCode,
            info               = connStatus?.Info,
            vendorId           = connStatus?.VendorId,
            vendorErrorCode    = connStatus?.VendorErrorCode,
            lastStatusUpdate   = connStatus?.LastStatusUpdate,
            activeTransactionId   = connStatus?.ActiveTransactionId,
            transactionStartTime  = connStatus?.TransactionStartTime,
            idTag              = connStatus?.IdTag,
            measurands
        };
    }
}

