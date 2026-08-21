using HASmartCharge.Backend.Services.Telemetry;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Forwards every telemetry callback to a fixed list of sinks. Each sink is invoked
/// independently with its own try/catch so one sink's failure never blocks, or breaks,
/// the others (and never throws back into the caller).
/// </summary>
public class TelemetryFanout : IChargerTelemetry
{
    private readonly IReadOnlyList<IChargerTelemetry> _sinks;
    private readonly ILogger<TelemetryFanout> _logger;

    public TelemetryFanout(IEnumerable<IChargerTelemetry> sinks, ILogger<TelemetryFanout> logger)
    {
        _sinks = sinks.ToList();
        _logger = logger;
    }

    public void OnConnected(string chargerId) =>
        ForEach(s => s.OnConnected(chargerId));

    public void OnDisconnected(string chargerId) =>
        ForEach(s => s.OnDisconnected(chargerId));

    public void OnChargerInfo(string chargerId, ChargerDeviceInfo info) =>
        ForEach(s => s.OnChargerInfo(chargerId, info));

    public void OnHeartbeat(string chargerId) =>
        ForEach(s => s.OnHeartbeat(chargerId));

    public void OnConnectorStatus(string chargerId, int connectorId, ConnectorState state, string? errorCode) =>
        ForEach(s => s.OnConnectorStatus(chargerId, connectorId, state, errorCode));

    public void OnSessionStarted(string chargerId, int connectorId, int sessionId, double meterStartKwh, string? tag, DateTimeOffset startedAt) =>
        ForEach(s => s.OnSessionStarted(chargerId, connectorId, sessionId, meterStartKwh, tag, startedAt));

    public void OnSessionStopped(string chargerId, int sessionId, double meterStopKwh, string? reason, DateTimeOffset stoppedAt) =>
        ForEach(s => s.OnSessionStopped(chargerId, sessionId, meterStopKwh, reason, stoppedAt));

    public void OnMeterSample(string chargerId, int connectorId, ChargerMeterSample sample) =>
        ForEach(s => s.OnMeterSample(chargerId, connectorId, sample));

    private void ForEach(Action<IChargerTelemetry> call)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                call(sink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry sink {Sink} threw handling a callback.", sink.GetType().Name);
            }
        }
    }
}
