using HASmartCharge.Application.Interfaces;
using HASmartCharge.Application.Queries.Models;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Read-only charger status and telemetry endpoints.
/// All data is sourced from the application read model.
/// </summary>
[ApiController]
[Route("api/chargers")]
[Produces("application/json")]
public class ChargersController : ControllerBase
{
    private readonly IChargerReadModel _chargerReadModel;

    public ChargersController(IChargerReadModel chargerReadModel)
    {
        _chargerReadModel = chargerReadModel;
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
        var list = _chargerReadModel.GetChargers(connected).ToList();

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
        var status = _chargerReadModel.GetCharger(chargerId);
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
        var status = _chargerReadModel.GetCharger(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        var connectors = status.Connectors
            .Select(MapConnectorDetail)
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
        var status = _chargerReadModel.GetCharger(chargerId);
        if (status == null)
            return NotFound(new { error = "Charger not found", chargerId });

        var connector = status.Connectors.FirstOrDefault(c => c.ConnectorId == connectorId);
        if (connector == null)
            return NotFound(new { error = "Connector not found", chargerId, connectorId });

        return Ok(MapConnectorDetail(connector));
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
        if (_chargerReadModel.GetCharger(chargerId) == null)
            return NotFound(new { error = "Charger not found", chargerId });

        var transactions = _chargerReadModel.GetActiveChargingSessions(chargerId)
            .OrderBy(session => session.ConnectorId)
            .Select(session => (object)new
            {
                connectorId = session.ConnectorId,
                transactionId = session.SessionId,
                idTag = session.AuthorizationTag,
                startTime = session.StartedAt,
                connectorStatus = session.ConnectorStatus,
                energyActiveImportWh = MapMeasurementValue(session.Measurements?.ImportedEnergy)
            })
            .ToList();

        return Ok(new { chargerId, count = transactions.Count, transactions });
    }

    // -------------------------------------------------------------------------
    // Mapping helpers
    // -------------------------------------------------------------------------

    private static object MapChargerSummary(ChargerSnapshot s) => new
    {
        chargePointId = s.ChargerId,
        isConnected = s.IsConnected,
        connectedAt = s.ConnectedAt,
        disconnectedAt = s.DisconnectedAt,
        lastUpdated = s.LastUpdated,
        vendor = s.Info?.Vendor,
        model = s.Info?.Model,
        firmwareVersion = s.Info?.FirmwareVersion,
        connectorCount = s.Connectors.Count
    };

    private static object MapChargerDetail(ChargerSnapshot s) => new
    {
        chargePointId = s.ChargerId,
        isConnected = s.IsConnected,
        connectedAt = s.ConnectedAt,
        disconnectedAt = s.DisconnectedAt,
        lastUpdated = s.LastUpdated,
        info = MapChargerInfo(s.Info),
        connectors = s.Connectors.Select(MapConnectorDetail)
    };

    private static object? MapChargerInfo(ChargerInfoSnapshot? info)
    {
        return info is null
            ? null
            : new
            {
                vendor = info.Vendor,
                model = info.Model,
                serialNumber = info.SerialNumber,
                firmwareVersion = info.FirmwareVersion,
                iccid = info.Iccid,
                imsi = info.Imsi,
                meterType = info.MeterType,
                meterSerialNumber = info.MeterSerialNumber
            };
    }

    private static object MapConnectorDetail(ConnectorSnapshot connector)
    {
        return new
        {
            connectorId = connector.ConnectorId,
            status = connector.Status,
            errorCode = connector.ErrorCode,
            info = connector.Info,
            vendorId = connector.VendorId,
            vendorErrorCode = connector.VendorErrorCode,
            lastStatusUpdate = connector.LastStatusUpdate,
            activeTransactionId = connector.ActiveSessionId,
            transactionStartTime = connector.SessionStartedAt,
            idTag = connector.AuthorizationTag,
            measurands = MapMeasurements(connector.Measurements)
        };
    }

    private static object? MapMeasurements(ConnectorMeasurementsSnapshot? measurements)
    {
        return measurements is null
            ? null
            : new
            {
                connectorId = measurements.ConnectorId,
                lastUpdated = measurements.LastUpdated,
                energyActiveImportRegister = MapMeasurementValue(measurements.ImportedEnergy),
                energyReactiveImportRegister = MapMeasurementValue(measurements.ImportedReactiveEnergy),
                energyActiveExportRegister = MapMeasurementValue(measurements.ExportedEnergy),
                energyReactiveExportRegister = MapMeasurementValue(measurements.ExportedReactiveEnergy),
                powerActiveImport = MapMeasurementValue(measurements.ImportedPower),
                powerReactiveImport = MapMeasurementValue(measurements.ImportedReactivePower),
                powerOffered = MapMeasurementValue(measurements.OfferedPower),
                voltageL1 = MapMeasurementValue(measurements.VoltageL1),
                voltageL2 = MapMeasurementValue(measurements.VoltageL2),
                voltageL3 = MapMeasurementValue(measurements.VoltageL3),
                voltageL1N = MapMeasurementValue(measurements.VoltageL1N),
                voltageL2N = MapMeasurementValue(measurements.VoltageL2N),
                voltageL3N = MapMeasurementValue(measurements.VoltageL3N),
                currentImportL1 = MapMeasurementValue(measurements.ImportedCurrentL1),
                currentImportL2 = MapMeasurementValue(measurements.ImportedCurrentL2),
                currentImportL3 = MapMeasurementValue(measurements.ImportedCurrentL3),
                currentExportL1 = MapMeasurementValue(measurements.ExportedCurrentL1),
                currentExportL2 = MapMeasurementValue(measurements.ExportedCurrentL2),
                currentExportL3 = MapMeasurementValue(measurements.ExportedCurrentL3),
                currentOffered = MapMeasurementValue(measurements.OfferedCurrent),
                temperature = MapMeasurementValue(measurements.Temperature),
                soC = MapMeasurementValue(measurements.StateOfCharge),
                frequency = MapMeasurementValue(measurements.Frequency),
                rpm = MapMeasurementValue(measurements.RevolutionsPerMinute)
            };
    }

    private static object? MapMeasurementValue(MeasurementValueSnapshot? value)
    {
        return value is null
            ? null
            : new
            {
                value = value.Value,
                unit = value.Unit,
                context = value.Context,
                format = value.Format,
                location = value.Location,
                phase = value.Phase,
                timestamp = value.Timestamp
            };
    }
}

