using System.Collections.Concurrent;
using System.Globalization;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Tracks the live status and measurands of connected chargers in memory.
/// Fed by the OCPP session layer via <see cref="IChargerTelemetrySink"/>.
/// Pure read model — no persistence, no domain events.
/// </summary>
public class ChargerStatusTracker : IChargerTelemetrySink
{
    private readonly ConcurrentDictionary<string, ChargerStatus> _chargerStatuses = new();
    private readonly ILogger<ChargerStatusTracker> _logger;

    public ChargerStatusTracker(ILogger<ChargerStatusTracker> logger)
    {
        _logger = logger;
    }

    #region IChargerTelemetrySink

    public void OnConnected(string chargePointId)
    {
        var status = GetOrAdd(chargePointId);
        status.IsConnected = true;
        status.ConnectedAt = DateTime.UtcNow;
        status.DisconnectedAt = null;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Charger {ChargePointId} marked as connected", chargePointId);
    }

    public void OnDisconnected(string chargePointId)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out var status))
        {
            status.IsConnected = false;
            status.DisconnectedAt = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;
            _logger.LogInformation("Charger {ChargePointId} marked as disconnected", chargePointId);
        }
    }

    public void OnBoot(string chargePointId, ChargerInfo info)
    {
        var status = GetOrAdd(chargePointId);
        status.Info = info;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Updated charger info for {ChargePointId}: {Vendor} {Model}",
            chargePointId, info.Vendor, info.Model);
    }

    public void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode)
    {
        var charger = GetOrAdd(chargePointId);
        var connector = charger.Connectors.GetOrAdd(connectorId, id => new ConnectorStatus { ConnectorId = id });
        connector.Status = status;
        connector.ErrorCode = errorCode ?? "NoError";
        connector.LastStatusUpdate = DateTime.UtcNow;
        charger.LastUpdated = DateTime.UtcNow;

        // Some chargers never send StopTransaction; they end a transaction by moving to a
        // terminal state. Clear the live transaction so status/live cost stop reflecting it
        // (ChargeSessionRecorder finalizes the persisted session off the same transition).
        if (connector.ActiveTransactionId is not null
            && status is "Finishing" or "Available" or "Faulted")
        {
            connector.ActiveTransactionId = null;
            connector.TransactionStartTime = null;
            connector.MeterStartKwh = null;
            connector.IdTag = null;
        }

        _logger.LogDebug("Updated status for {ChargePointId} connector {ConnectorId}: {Status}",
            chargePointId, connectorId, status);
    }

    public void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt)
    {
        var charger = GetOrAdd(chargePointId);
        var connector = charger.Connectors.GetOrAdd(connectorId, id => new ConnectorStatus { ConnectorId = id });
        connector.ActiveTransactionId = transactionId;
        connector.TransactionStartTime = startedAt.UtcDateTime;
        connector.MeterStartKwh = meterStartWh / 1000.0;
        connector.IdTag = idTag;
        charger.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Transaction {TransactionId} started on {ChargePointId} connector {ConnectorId}",
            transactionId, chargePointId, connectorId);
    }

    public void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt)
    {
        if (_chargerStatuses.TryGetValue(chargePointId, out var charger))
        {
            var connector = charger.Connectors.Values.FirstOrDefault(c => c.ActiveTransactionId == transactionId);
            if (connector != null)
            {
                connector.ActiveTransactionId = null;
                connector.TransactionStartTime = null;
                connector.MeterStartKwh = null;
                connector.IdTag = null;
                charger.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation("Transaction {TransactionId} stopped on {ChargePointId} connector {ConnectorId}",
                    transactionId, chargePointId, connector.ConnectorId);
            }
        }
    }

    public void OnMeterValues(string chargePointId, MeterValuesRequest request)
    {
        var status = GetOrAdd(chargePointId);
        var measurands = status.Measurands.GetOrAdd(request.ConnectorId, id => new ConnectorMeasurands { ConnectorId = id });

        foreach (var meterValue in request.MeterValue)
            foreach (var sampledValue in meterValue.SampledValue)
                UpdateMeasurand(measurands, sampledValue, meterValue.Timestamp);

        measurands.LastUpdated = DateTime.UtcNow;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogDebug("Updated measurands for {ChargePointId} connector {ConnectorId}",
            chargePointId, request.ConnectorId);
    }

    #endregion

    private ChargerStatus GetOrAdd(string chargePointId) =>
        _chargerStatuses.GetOrAdd(chargePointId, id => new ChargerStatus { ChargePointId = id });

    private void UpdateMeasurand(ConnectorMeasurands measurands, SampledValue sampledValue, DateTime timestamp)
    {
        var value = new MeasurandValue
        {
            Value = sampledValue.Value,
            Unit = sampledValue.Unit,
            Context = sampledValue.Context,
            Format = sampledValue.Format,
            Location = sampledValue.Location,
            Phase = sampledValue.Phase,
            Timestamp = timestamp
        };

        var measurand = sampledValue.Measurand ?? "Energy.Active.Import.Register";
        var phase = sampledValue.Phase;

        switch (measurand)
        {
            case "Energy.Active.Import.Register":
                // OCPP 1.6: a missing unit means Wh for energy measurands.
                if (value.Unit is null || value.Unit.Equals("wh", StringComparison.OrdinalIgnoreCase))
                {
                    value.Value = (float.Parse(value.Value, CultureInfo.InvariantCulture) / 1000f).ToString(CultureInfo.InvariantCulture); // Wh -> kWh
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

            case "Power.Active.Import":
                measurands.PowerActiveImport = value;
                break;
            case "Power.Reactive.Import":
                measurands.PowerReactiveImport = value;
                break;
            case "Power.Offered":
                measurands.PowerOffered = value;
                break;

            case "Voltage":
                switch (phase)
                {
                    case "L1": measurands.VoltageL1 = value; break;
                    case "L2": measurands.VoltageL2 = value; break;
                    case "L3": measurands.VoltageL3 = value; break;
                    case "L1-N": measurands.VoltageL1N = value; break;
                    case "L2-N": measurands.VoltageL2N = value; break;
                    case "L3-N": measurands.VoltageL3N = value; break;
                }
                break;

            case "Current.Import":
                switch (phase)
                {
                    case "L1": measurands.CurrentImportL1 = value; break;
                    case "L2": measurands.CurrentImportL2 = value; break;
                    case "L3": measurands.CurrentImportL3 = value; break;
                }
                break;
            case "Current.Export":
                switch (phase)
                {
                    case "L1": measurands.CurrentExportL1 = value; break;
                    case "L2": measurands.CurrentExportL2 = value; break;
                    case "L3": measurands.CurrentExportL3 = value; break;
                }
                break;
            case "Current.Offered":
                measurands.CurrentOffered = value;
                break;

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
                _logger.LogWarning("Unrecognized measurand {Measurand} for connector {ConnectorId}",
                    measurand, measurands.ConnectorId);
                break;
        }
    }

    #region Query methods (raw in-memory status — consumed by the read API in later phases)

    public ChargerStatus? GetChargerStatus(string chargePointId) =>
        _chargerStatuses.TryGetValue(chargePointId, out var status) ? status : null;

    public IEnumerable<ChargerStatus> GetAllChargerStatuses() => _chargerStatuses.Values;

    public IEnumerable<ChargerStatus> GetConnectedChargers() => _chargerStatuses.Values.Where(s => s.IsConnected);

    public ConnectorStatus? GetConnectorStatus(string chargePointId, int connectorId) =>
        _chargerStatuses.TryGetValue(chargePointId, out var status)
            && status.Connectors.TryGetValue(connectorId, out var connector)
            ? connector
            : null;

    public ConnectorMeasurands? GetConnectorMeasurands(string chargePointId, int connectorId) =>
        _chargerStatuses.TryGetValue(chargePointId, out var status)
            && status.Measurands.TryGetValue(connectorId, out var measurands)
            ? measurands
            : null;

    public void RemoveCharger(string chargePointId)
    {
        if (_chargerStatuses.TryRemove(chargePointId, out _))
            _logger.LogInformation("Removed charger {ChargePointId} from status tracking", chargePointId);
    }

    #endregion
}
