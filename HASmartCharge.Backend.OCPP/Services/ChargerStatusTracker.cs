using System.Collections.Concurrent;
using System.Globalization;
using HASmartCharge.Application.Interfaces;
using HASmartCharge.Application.Queries.Models;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service that tracks the status and measurands of all connected chargers
/// </summary>
public class ChargerStatusTracker : IChargerReadModel
{
    private readonly ConcurrentDictionary<string, ChargerStatus> _chargerStatuses = new();
    private readonly ILogger<ChargerStatusTracker> _logger;

    public ChargerStatusTracker(ILogger<ChargerStatusTracker> logger)
    {
        _logger = logger;
    }

    #region Startup Seeding

    /// <summary>
    /// Seed the in-memory tracker from domain charger entities.
    /// Called once at startup so the API shows all known chargers (as disconnected)
    /// before any WebSocket connections arrive.
    /// </summary>
    public void SeedFromDomainChargers(IReadOnlyList<HASmartCharge.Domain.Entities.Charger> chargers)
    {
        int count = 0;
        foreach (HASmartCharge.Domain.Entities.Charger charger in chargers)
        {
            ChargerStatus status = _chargerStatuses.GetOrAdd(charger.ChargePointId, id => new ChargerStatus
            {
                ChargePointId = id
            });

            // Mark as disconnected — a live WebSocket connect will flip this to true
            status.IsConnected = false;
            status.LastUpdated = charger.LastConnectedAt?.UtcDateTime ?? DateTime.UtcNow;
            status.ConnectedAt = charger.LastConnectedAt?.UtcDateTime;

            // Populate boot info if we have it
            if (!string.IsNullOrEmpty(charger.Vendor) || !string.IsNullOrEmpty(charger.Model))
            {
                status.Info = new ChargerInfo
                {
                    Vendor = charger.Vendor,
                    Model = charger.Model,
                    SerialNumber = charger.SerialNumber,
                    FirmwareVersion = charger.FirmwareVersion
                };
            }

            // Populate connector statuses
            foreach (HASmartCharge.Domain.Entities.Connector connector in charger.Connectors)
            {
                ConnectorStatus connectorStatus = status.Connectors.GetOrAdd(connector.ConnectorId, id => new ConnectorStatus
                {
                    ConnectorId = id
                });

                connectorStatus.Status = connector.Status ?? "Unknown";
                connectorStatus.ErrorCode = connector.ErrorCode ?? "NoError";
            }

            count++;
        }

        _logger.LogInformation("Seeded {Count} chargers from domain entities into status tracker", count);
    }

    #endregion

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
                if (value.Unit?.Equals("wh", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    value.Value = (float.Parse(value.Value) / 1000f).ToString(CultureInfo.InvariantCulture); // Convert Wh to kWh
                    value.Unit = "kWh";
                }
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
                measurands.Rpm = value;
                break;
            default:
                //Log unrecognized measurand for debugging
                _logger.LogWarning("Received unrecognized measurand {Measurand} for {ChargePointId} connector {ConnectorId}",
                    measurand, measurands.ConnectorId, measurands.ConnectorId);
                break;
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Returns immutable charger snapshots for the API/application read model.
    /// </summary>
    public IEnumerable<ChargerSnapshot> GetChargers(bool? connected = null)
    {
        IEnumerable<ChargerStatus> statuses = connected switch
        {
            true => GetConnectedChargers(),
            false => GetAllChargerStatuses().Where(status => !status.IsConnected),
            null => GetAllChargerStatuses()
        };

        return statuses.Select(CreateChargerSnapshot);
    }

    /// <summary>
    /// Returns a single immutable charger snapshot for the API/application read model.
    /// </summary>
    public ChargerSnapshot? GetCharger(string chargerId)
    {
        return GetChargerStatus(chargerId) is { } status
            ? CreateChargerSnapshot(status)
            : null;
    }

    /// <summary>
    /// Returns active charging-session snapshots for the API/application read model.
    /// </summary>
    public IEnumerable<ActiveChargingSessionSnapshot> GetActiveChargingSessions(string? chargerId = null)
    {
        IEnumerable<ChargerStatus> statuses = string.IsNullOrWhiteSpace(chargerId)
            ? GetAllChargerStatuses()
            : _chargerStatuses.TryGetValue(chargerId, out ChargerStatus? status)
                ? new[] { status }
                : Enumerable.Empty<ChargerStatus>();

        return statuses.SelectMany(status => status.Connectors.Values
            .Where(connector => connector.ActiveTransactionId.HasValue)
            .OrderBy(connector => connector.ConnectorId)
            .Select(connector => CreateActiveChargingSessionSnapshot(status, connector)));
    }

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
    /// Get all active transactions across every charger, including measurand data.
    /// Used by the dashboard summary endpoint.
    /// </summary>
    public IEnumerable<(string ChargePointId, ConnectorStatus Connector, ConnectorMeasurands? Measurands)> GetAllActiveTransactions()
    {
        return _chargerStatuses.Values
            .SelectMany(status => status.Connectors.Values
                .Where(c => c.ActiveTransactionId.HasValue)
                .Select(c =>
                {
                    status.Measurands.TryGetValue(c.ConnectorId, out ConnectorMeasurands? measurands);
                    return (status.ChargePointId, c, measurands);
                }));
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

    private ChargerSnapshot CreateChargerSnapshot(ChargerStatus status)
    {
        List<ConnectorSnapshot> connectors = status.Connectors.Values
            .OrderBy(connector => connector.ConnectorId)
            .Select(connector => CreateConnectorSnapshot(status, connector))
            .ToList();

        return new ChargerSnapshot
        {
            ChargerId = status.ChargePointId,
            LastUpdated = status.LastUpdated,
            IsConnected = status.IsConnected,
            ConnectedAt = status.ConnectedAt,
            DisconnectedAt = status.DisconnectedAt,
            Info = CreateChargerInfoSnapshot(status.Info),
            Connectors = connectors
        };
    }

    private static ChargerInfoSnapshot? CreateChargerInfoSnapshot(ChargerInfo? info)
    {
        return info is null
            ? null
            : new ChargerInfoSnapshot
            {
                Vendor = info.Vendor,
                Model = info.Model,
                SerialNumber = info.SerialNumber,
                FirmwareVersion = info.FirmwareVersion,
                Iccid = info.Iccid,
                Imsi = info.Imsi,
                MeterType = info.MeterType,
                MeterSerialNumber = info.MeterSerialNumber
            };
    }

    private ConnectorSnapshot CreateConnectorSnapshot(ChargerStatus status, ConnectorStatus connector)
    {
        status.Measurands.TryGetValue(connector.ConnectorId, out ConnectorMeasurands? measurands);

        return new ConnectorSnapshot
        {
            ConnectorId = connector.ConnectorId,
            Status = connector.Status,
            ErrorCode = connector.ErrorCode,
            Info = connector.Info,
            VendorId = connector.VendorId,
            VendorErrorCode = connector.VendorErrorCode,
            LastStatusUpdate = connector.LastStatusUpdate,
            ActiveSessionId = connector.ActiveTransactionId,
            SessionStartedAt = connector.TransactionStartTime,
            AuthorizationTag = connector.IdTag,
            Measurements = CreateConnectorMeasurementsSnapshot(measurands)
        };
    }

    private ActiveChargingSessionSnapshot CreateActiveChargingSessionSnapshot(ChargerStatus status, ConnectorStatus connector)
    {
        status.Measurands.TryGetValue(connector.ConnectorId, out ConnectorMeasurands? measurands);

        return new ActiveChargingSessionSnapshot
        {
            ChargerId = status.ChargePointId,
            ConnectorId = connector.ConnectorId,
            SessionId = connector.ActiveTransactionId!.Value,
            AuthorizationTag = connector.IdTag,
            StartedAt = connector.TransactionStartTime,
            ConnectorStatus = connector.Status,
            Measurements = CreateConnectorMeasurementsSnapshot(measurands)
        };
    }

    private static ConnectorMeasurementsSnapshot? CreateConnectorMeasurementsSnapshot(ConnectorMeasurands? measurands)
    {
        return measurands is null
            ? null
            : new ConnectorMeasurementsSnapshot
            {
                ConnectorId = measurands.ConnectorId,
                LastUpdated = measurands.LastUpdated,
                ImportedEnergy = CreateMeasurementValueSnapshot(measurands.EnergyActiveImportRegister),
                ImportedReactiveEnergy = CreateMeasurementValueSnapshot(measurands.EnergyReactiveImportRegister),
                ExportedEnergy = CreateMeasurementValueSnapshot(measurands.EnergyActiveExportRegister),
                ExportedReactiveEnergy = CreateMeasurementValueSnapshot(measurands.EnergyReactiveExportRegister),
                ImportedPower = CreateMeasurementValueSnapshot(measurands.PowerActiveImport),
                ImportedReactivePower = CreateMeasurementValueSnapshot(measurands.PowerReactiveImport),
                OfferedPower = CreateMeasurementValueSnapshot(measurands.PowerOffered),
                VoltageL1 = CreateMeasurementValueSnapshot(measurands.VoltageL1),
                VoltageL2 = CreateMeasurementValueSnapshot(measurands.VoltageL2),
                VoltageL3 = CreateMeasurementValueSnapshot(measurands.VoltageL3),
                VoltageL1N = CreateMeasurementValueSnapshot(measurands.VoltageL1N),
                VoltageL2N = CreateMeasurementValueSnapshot(measurands.VoltageL2N),
                VoltageL3N = CreateMeasurementValueSnapshot(measurands.VoltageL3N),
                ImportedCurrentL1 = CreateMeasurementValueSnapshot(measurands.CurrentImportL1),
                ImportedCurrentL2 = CreateMeasurementValueSnapshot(measurands.CurrentImportL2),
                ImportedCurrentL3 = CreateMeasurementValueSnapshot(measurands.CurrentImportL3),
                ExportedCurrentL1 = CreateMeasurementValueSnapshot(measurands.CurrentExportL1),
                ExportedCurrentL2 = CreateMeasurementValueSnapshot(measurands.CurrentExportL2),
                ExportedCurrentL3 = CreateMeasurementValueSnapshot(measurands.CurrentExportL3),
                OfferedCurrent = CreateMeasurementValueSnapshot(measurands.CurrentOffered),
                Temperature = CreateMeasurementValueSnapshot(measurands.Temperature),
                StateOfCharge = CreateMeasurementValueSnapshot(measurands.SoC),
                Frequency = CreateMeasurementValueSnapshot(measurands.Frequency),
                RevolutionsPerMinute = CreateMeasurementValueSnapshot(measurands.Rpm)
            };
    }

    private static MeasurementValueSnapshot? CreateMeasurementValueSnapshot(MeasurandValue? value)
    {
        return value is null
            ? null
            : new MeasurementValueSnapshot
            {
                Value = value.Value,
                Unit = value.Unit,
                Context = value.Context,
                Format = value.Format,
                Location = value.Location,
                Phase = value.Phase,
                Timestamp = value.Timestamp
            };
    }

    #endregion
}

