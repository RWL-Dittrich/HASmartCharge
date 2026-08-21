namespace HASmartCharge.Backend.Services.Telemetry;

/// <summary>
/// Charger-neutral telemetry contract. Consumed by <see cref="ChargerStatusTracker"/>,
/// <see cref="ChargeSessionRecorder"/> and the MQTT nudge — none of them know whether the events
/// originated from OCPP or from a cloud-polled charger (Zaptec). All OCPP parsing and unit quirks
/// (missing-unit-means-Wh, W vs kW, ...) are concentrated in <see cref="OcppTelemetryAdapter"/>,
/// which implements the OCPP-shaped <c>IChargerTelemetrySink</c> and translates into this contract.
/// A future Zaptec poller emits this contract directly, with no OCPP shapes involved.
/// </summary>
public interface IChargerTelemetry
{
    void OnConnected(string chargerId);
    void OnDisconnected(string chargerId);
    void OnChargerInfo(string chargerId, ChargerDeviceInfo info);
    void OnHeartbeat(string chargerId);
    void OnConnectorStatus(string chargerId, int connectorId, ConnectorState state, string? errorCode);
    void OnSessionStarted(string chargerId, int connectorId, int sessionId, double meterStartKwh, string? tag, DateTimeOffset startedAt);
    void OnSessionStopped(string chargerId, int sessionId, double meterStopKwh, string? reason, DateTimeOffset stoppedAt);
    void OnMeterSample(string chargerId, int connectorId, ChargerMeterSample sample);
}
