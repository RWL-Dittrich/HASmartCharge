using System.Collections.Concurrent;

namespace HASmartCharge.Backend.Services.Telemetry;

/// <summary>
/// Tracks the live status and measurands of connected chargers in memory.
/// Fed by <see cref="IChargerTelemetry"/> (OCPP today via <see cref="OcppTelemetryAdapter"/>,
/// a Zaptec poller later — this class has no OCPP-specific knowledge).
/// Pure read model — no persistence, no domain events.
/// </summary>
public class ChargerStatusTracker : IChargerTelemetry
{
    private readonly ConcurrentDictionary<string, ChargerStatus> _chargerStatuses = new();
    private readonly ILogger<ChargerStatusTracker> _logger;

    public ChargerStatusTracker(ILogger<ChargerStatusTracker> logger)
    {
        _logger = logger;
    }

    #region IChargerTelemetry

    public void OnConnected(string chargerId)
    {
        var status = GetOrAdd(chargerId);
        status.IsConnected = true;
        status.ConnectedAt = DateTime.UtcNow;
        status.DisconnectedAt = null;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Charger {ChargerId} marked as connected", chargerId);
    }

    public void OnDisconnected(string chargerId)
    {
        if (_chargerStatuses.TryGetValue(chargerId, out var status))
        {
            status.IsConnected = false;
            status.DisconnectedAt = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;
            _logger.LogInformation("Charger {ChargerId} marked as disconnected", chargerId);
        }
    }

    public void OnChargerInfo(string chargerId, ChargerDeviceInfo info)
    {
        var status = GetOrAdd(chargerId);
        status.Info = info;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Updated charger info for {ChargerId}: {Vendor} {Model}",
            chargerId, info.Vendor, info.Model);
    }

    public void OnHeartbeat(string chargerId)
    {
        var status = GetOrAdd(chargerId);
        var now = DateTime.UtcNow;
        status.LastHeartbeat = now;
        status.LastUpdated = now;
        _logger.LogDebug("Heartbeat from {ChargerId}", chargerId);
    }

    public void OnConnectorStatus(string chargerId, int connectorId, ConnectorState state, string? errorCode)
    {
        var charger = GetOrAdd(chargerId);
        var connector = charger.Connectors.GetOrAdd(connectorId, id => new ConnectorStatus { ConnectorId = id });
        connector.Status = state.ToString();
        connector.ErrorCode = errorCode ?? "NoError";
        connector.LastStatusUpdate = DateTime.UtcNow;
        charger.LastUpdated = DateTime.UtcNow;

        // Some chargers never send a stop event; they end a session by moving to a terminal
        // state. Clear the live transaction so status/live cost stop reflecting it
        // (ChargeSessionRecorder finalizes the persisted session off the same transition).
        if (connector.ActiveTransactionId is not null
            && state is ConnectorState.Finishing or ConnectorState.Available or ConnectorState.Faulted)
        {
            connector.ActiveTransactionId = null;
            connector.TransactionStartTime = null;
            connector.MeterStartKwh = null;
            connector.IdTag = null;
        }

        _logger.LogDebug("Updated status for {ChargerId} connector {ConnectorId}: {Status}",
            chargerId, connectorId, connector.Status);
    }

    public void OnSessionStarted(string chargerId, int connectorId, int sessionId, double meterStartKwh, string? tag, DateTimeOffset startedAt)
    {
        var charger = GetOrAdd(chargerId);
        var connector = charger.Connectors.GetOrAdd(connectorId, id => new ConnectorStatus { ConnectorId = id });
        connector.ActiveTransactionId = sessionId;
        connector.TransactionStartTime = startedAt.UtcDateTime;
        connector.MeterStartKwh = meterStartKwh;
        connector.IdTag = tag;
        charger.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation("Session {SessionId} started on {ChargerId} connector {ConnectorId}",
            sessionId, chargerId, connectorId);
    }

    public void OnSessionStopped(string chargerId, int sessionId, double meterStopKwh, string? reason, DateTimeOffset stoppedAt)
    {
        if (_chargerStatuses.TryGetValue(chargerId, out var charger))
        {
            var connector = charger.Connectors.Values.FirstOrDefault(c => c.ActiveTransactionId == sessionId);
            if (connector != null)
            {
                connector.ActiveTransactionId = null;
                connector.TransactionStartTime = null;
                connector.MeterStartKwh = null;
                connector.IdTag = null;
                charger.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation("Session {SessionId} stopped on {ChargerId} connector {ConnectorId}",
                    sessionId, chargerId, connector.ConnectorId);
            }
        }
    }

    public void OnMeterSample(string chargerId, int connectorId, ChargerMeterSample sample)
    {
        var status = GetOrAdd(chargerId);
        var measurands = status.Measurands.GetOrAdd(connectorId, id => new ConnectorMeasurands { ConnectorId = id });

        // Partial update: a sample only carries the measurands its source actually reported at
        // that instant, so a null field here means "unchanged", not "cleared".
        if (sample.EnergyRegisterKwh is { } energy) measurands.EnergyRegisterKwh = energy;
        if (sample.PowerKw is { } power) measurands.PowerKw = power;
        if (sample.VoltageL1 is { } v1) measurands.VoltageL1 = v1;
        if (sample.VoltageL2 is { } v2) measurands.VoltageL2 = v2;
        if (sample.VoltageL3 is { } v3) measurands.VoltageL3 = v3;
        if (sample.CurrentL1 is { } c1) measurands.CurrentL1 = c1;
        if (sample.CurrentL2 is { } c2) measurands.CurrentL2 = c2;
        if (sample.CurrentL3 is { } c3) measurands.CurrentL3 = c3;
        if (sample.SocPercent is { } soc) measurands.SocPercent = soc;

        measurands.LastUpdated = DateTime.UtcNow;
        status.LastUpdated = DateTime.UtcNow;
        _logger.LogDebug("Updated measurands for {ChargerId} connector {ConnectorId}", chargerId, connectorId);
    }

    #endregion

    /// <summary>
    /// Restores the live transaction of a connector after a backend restart. The tracker is a pure
    /// in-memory read model, so a mid-charge restart otherwise loses the active transaction until
    /// the charger re-announces it. Seeded from the still-open DB session so the dashboard's live
    /// session energy/cost tiles keep working across the restart. Overwritten as soon as the
    /// charger sends a fresh StartTransaction or moves the connector to a terminal state.
    /// </summary>
    public void SeedActiveTransaction(string chargerId, int connectorId, int transactionId, double meterStartKwh, DateTime transactionStartTimeUtc)
    {
        var charger = GetOrAdd(chargerId);
        var connector = charger.Connectors.GetOrAdd(connectorId, id => new ConnectorStatus { ConnectorId = id });
        connector.ActiveTransactionId = transactionId;
        connector.TransactionStartTime = DateTime.SpecifyKind(transactionStartTimeUtc, DateTimeKind.Utc);
        connector.MeterStartKwh = meterStartKwh;
        charger.LastUpdated = DateTime.UtcNow;
        _logger.LogInformation(
            "Seeded active transaction {TransactionId} on {ChargerId} connector {ConnectorId} after restart",
            transactionId, chargerId, connectorId);
    }

    private ChargerStatus GetOrAdd(string chargerId) =>
        _chargerStatuses.GetOrAdd(chargerId, id => new ChargerStatus { ChargerId = id });

    #region Query methods (raw in-memory status — consumed by the read API in later phases)

    public ChargerStatus? GetChargerStatus(string chargerId) =>
        _chargerStatuses.TryGetValue(chargerId, out var status) ? status : null;

    public IEnumerable<ChargerStatus> GetAllChargerStatuses() => _chargerStatuses.Values;

    public IEnumerable<ChargerStatus> GetConnectedChargers() => _chargerStatuses.Values.Where(s => s.IsConnected);

    public ConnectorStatus? GetConnectorStatus(string chargerId, int connectorId) =>
        _chargerStatuses.TryGetValue(chargerId, out var status)
            && status.Connectors.TryGetValue(connectorId, out var connector)
            ? connector
            : null;

    public ConnectorMeasurands? GetConnectorMeasurands(string chargerId, int connectorId) =>
        _chargerStatuses.TryGetValue(chargerId, out var status)
            && status.Measurands.TryGetValue(connectorId, out var measurands)
            ? measurands
            : null;

    public void RemoveCharger(string chargerId)
    {
        if (_chargerStatuses.TryRemove(chargerId, out _))
            _logger.LogInformation("Removed charger {ChargerId} from status tracking", chargerId);
    }

    #endregion
}
