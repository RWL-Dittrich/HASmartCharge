using System.Collections.Concurrent;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service that tracks the status and measurands of all connected chargers
/// </summary>
public class ChargerStatusTracker
{
    private readonly ConcurrentDictionary<string, ChargerStatus> _chargerStatuses = new();
    private readonly ILogger<ChargerStatusTracker> _logger;

    public ChargerStatusTracker(ILogger<ChargerStatusTracker> logger)
    {
        _logger = logger;
    }

    #region Connection Management

    /// <summary>
    /// Mark a charger as connected
    /// </summary>
    public void OnChargerConnected(string chargePointId)
    {
        ChargerStatus status = _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus 
        { 
            ChargePointId = id 
        });

        status.IsConnected = true;
        status.ConnectedAt = DateTime.UtcNow;
        status.DisconnectedAt = null;
        status.LastUpdated = DateTime.UtcNow;

        _logger.LogInformation("Charger {ChargePointId} marked as connected", chargePointId);
    }

    /// <summary>
    /// Mark a charger as disconnected
    /// </summary>
    public void OnChargerDisconnected(string chargePointId)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out ChargerStatus? status))
        {
            status.IsConnected = false;
            status.DisconnectedAt = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Charger {ChargePointId} marked as disconnected", chargePointId);
        }
    }

    #endregion

    #region Boot Notification

    /// <summary>
    /// Update charger info from BootNotification
    /// </summary>
    public void OnBootNotification(string chargePointId, BootNotificationRequest request)
    {
        ChargerStatus status = _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus 
        { 
            ChargePointId = id,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow
        });

        status.Info = new ChargerInfo
        {
            Vendor = request.ChargePointVendor,
            Model = request.ChargePointModel,
            SerialNumber = request.ChargePointSerialNumber,
            FirmwareVersion = request.FirmwareVersion,
            Iccid = request.Iccid,
            Imsi = request.Imsi,
            MeterType = request.MeterType,
            MeterSerialNumber = request.MeterSerialNumber
        };

        status.LastUpdated = DateTime.UtcNow;

        _logger.LogInformation("Updated charger info for {ChargePointId}: {Vendor} {Model}", 
            chargePointId, request.ChargePointVendor, request.ChargePointModel);
    }

    #endregion

    #region Status Notification

    /// <summary>
    /// Update connector status from StatusNotification
    /// </summary>
    public void OnStatusNotification(string chargePointId, StatusNotificationRequest request)
    {
        ChargerStatus status = _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus 
        { 
            ChargePointId = id,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow
        });

        ConnectorStatus connectorStatus = status.Connectors.GetOrAdd(request.ConnectorId, id => new ConnectorStatus 
        { 
            ConnectorId = id 
        });

        connectorStatus.Status = request.Status;
        connectorStatus.ErrorCode = request.ErrorCode;
        connectorStatus.Info = request.Info;
        connectorStatus.VendorId = request.VendorId;
        connectorStatus.VendorErrorCode = request.VendorErrorCode;
        connectorStatus.LastStatusUpdate = DateTime.UtcNow;

        status.LastUpdated = DateTime.UtcNow;

        _logger.LogDebug("Updated status for {ChargePointId} connector {ConnectorId}: {Status}", 
            chargePointId, request.ConnectorId, request.Status);
    }

    #endregion

    #region Transaction Management

    /// <summary>
    /// Update connector status when transaction starts
    /// </summary>
    public void OnStartTransaction(string chargePointId, StartTransactionRequest request, int transactionId)
    {
        ChargerStatus status = _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus 
        { 
            ChargePointId = id,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow
        });

        ConnectorStatus connectorStatus = status.Connectors.GetOrAdd(request.ConnectorId, id => new ConnectorStatus 
        { 
            ConnectorId = id 
        });

        connectorStatus.ActiveTransactionId = transactionId;
        connectorStatus.TransactionStartTime = request.Timestamp;
        connectorStatus.IdTag = request.IdTag;

        status.LastUpdated = DateTime.UtcNow;

        _logger.LogInformation("Transaction {TransactionId} started on {ChargePointId} connector {ConnectorId}", 
            transactionId, chargePointId, request.ConnectorId);
    }

    /// <summary>
    /// Update connector status when transaction stops
    /// </summary>
    public void OnStopTransaction(string chargePointId, StopTransactionRequest request)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out ChargerStatus? status))
        {
            // Find the connector with this transaction ID
            ConnectorStatus? connector = status.Connectors.Values
                .FirstOrDefault(c => c.ActiveTransactionId == request.TransactionId);

            if (connector != null)
            {
                connector.ActiveTransactionId = null;
                connector.TransactionStartTime = null;
                connector.IdTag = null;

                status.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation("Transaction {TransactionId} stopped on {ChargePointId} connector {ConnectorId}", 
                    request.TransactionId, chargePointId, connector.ConnectorId);
            }
        }
    }

    #endregion

    #region Meter Values

    /// <summary>
    /// Update measurands from MeterValues
    /// </summary>
    public void OnMeterValues(string chargePointId, MeterValuesRequest request)
    {
        ChargerStatus status = _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus 
        { 
            ChargePointId = id,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow
        });

        ConnectorMeasurands measurands = status.Measurands.GetOrAdd(request.ConnectorId, id => new ConnectorMeasurands 
        { 
            ConnectorId = id 
        });

        // Process each meter value
        foreach (MeterValue meterValue in request.MeterValue)
        {
            foreach (SampledValue sampledValue in meterValue.SampledValue)
            {
                UpdateMeasurand(measurands, sampledValue, meterValue.Timestamp);
            }
        }

        measurands.LastUpdated = DateTime.UtcNow;
        status.LastUpdated = DateTime.UtcNow;

        _logger.LogDebug("Updated measurands for {ChargePointId} connector {ConnectorId}", 
            chargePointId, request.ConnectorId);
    }

    private void UpdateMeasurand(ConnectorMeasurands measurands, SampledValue sampledValue, DateTime timestamp)
    {
        MeasurandValue value = new MeasurandValue
        {
            Value = sampledValue.Value,
            Unit = sampledValue.Unit,
            Context = sampledValue.Context,
            Format = sampledValue.Format,
            Location = sampledValue.Location,
            Phase = sampledValue.Phase,
            Timestamp = timestamp
        };

        // Map measurand to the appropriate property
        string measurand = sampledValue.Measurand ?? "Energy.Active.Import.Register";
        string? phase = sampledValue.Phase;

        switch (measurand)
        {
            // Energy
            case "Energy.Active.Import.Register":
                measurands.EnergyActiveImportRegister = value;
                break;
            case "Energy.Reactive.Import.Register":
                measurands.EnergyReactiveImportRegister = value;
                break;
            case "Energy.Active.Export.Register":
                measurands.EnergyActiveExportRegister = value;
                break;
            case "Energy.Reactive.Export.Register":
                measurands.EnergyReactiveExportRegister = value;
                break;

            // Power
            case "Power.Active.Import":
                measurands.PowerActiveImport = value;
                break;
            case "Power.Reactive.Import":
                measurands.PowerReactiveImport = value;
                break;
            case "Power.Offered":
                measurands.PowerOffered = value;
                break;

            // Voltage
            case "Voltage":
                if (phase == "L1")
                    measurands.VoltageL1 = value;
                else if (phase == "L2")
                    measurands.VoltageL2 = value;
                else if (phase == "L3")
                    measurands.VoltageL3 = value;
                else if (phase == "L1-N")
                    measurands.VoltageL1N = value;
                else if (phase == "L2-N")
                    measurands.VoltageL2N = value;
                else if (phase == "L3-N")
                    measurands.VoltageL3N = value;
                break;

            // Current
            case "Current.Import":
                if (phase == "L1")
                    measurands.CurrentImportL1 = value;
                else if (phase == "L2")
                    measurands.CurrentImportL2 = value;
                else if (phase == "L3")
                    measurands.CurrentImportL3 = value;
                break;
            case "Current.Export":
                if (phase == "L1")
                    measurands.CurrentExportL1 = value;
                else if (phase == "L2")
                    measurands.CurrentExportL2 = value;
                else if (phase == "L3")
                    measurands.CurrentExportL3 = value;
                break;
            case "Current.Offered":
                measurands.CurrentOffered = value;
                break;

            // Other
            case "Temperature":
                measurands.Temperature = value;
                break;
            case "SoC":
                measurands.SoC = value;
                break;
            case "Frequency":
                measurands.Frequency = value;
                break;
            case "RPM":
                measurands.RPM = value;
                break;
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Get status of a specific charger
    /// </summary>
    public ChargerStatus? GetChargerStatus(string chargePointId)
    {
        return _chargerStatuses.TryGetValue(chargePointId, out ChargerStatus? status) ? status : null;
    }

    /// <summary>
    /// Get all charger statuses
    /// </summary>
    public IEnumerable<ChargerStatus> GetAllChargerStatuses()
    {
        return _chargerStatuses.Values;
    }

    /// <summary>
    /// Get all connected chargers
    /// </summary>
    public IEnumerable<ChargerStatus> GetConnectedChargers()
    {
        return _chargerStatuses.Values.Where(s => s.IsConnected);
    }

    /// <summary>
    /// Get connector status for a specific charger and connector
    /// </summary>
    public ConnectorStatus? GetConnectorStatus(string chargePointId, int connectorId)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out ChargerStatus? status))
        {
            return status.Connectors.TryGetValue(connectorId, out ConnectorStatus? connectorStatus) 
                ? connectorStatus 
                : null;
        }
        return null;
    }

    /// <summary>
    /// Get measurands for a specific charger and connector
    /// </summary>
    public ConnectorMeasurands? GetConnectorMeasurands(string chargePointId, int connectorId)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out ChargerStatus? status))
        {
            return status.Measurands.TryGetValue(connectorId, out ConnectorMeasurands? measurands) 
                ? measurands 
                : null;
        }
        return null;
    }

    /// <summary>
    /// Get all chargers currently in a charging state
    /// </summary>
    public IEnumerable<(string ChargePointId, int ConnectorId)> GetActiveChargingSessions()
    {
        return _chargerStatuses.Values
            .SelectMany(status => status.Connectors.Values
                .Where(c => c.Status == "Charging" && c.ActiveTransactionId.HasValue)
                .Select(c => (status.ChargePointId, c.ConnectorId)));
    }

    /// <summary>
    /// Remove a charger from tracking (cleanup)
    /// </summary>
    public void RemoveCharger(string chargePointId)
    {
        if (_chargerStatuses.TryRemove(chargePointId, out _))
        {
            _logger.LogInformation("Removed charger {ChargePointId} from status tracking", chargePointId);
        }
    }

    #endregion
}

