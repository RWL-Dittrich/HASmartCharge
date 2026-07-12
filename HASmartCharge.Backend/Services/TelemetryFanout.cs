using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Forwards every telemetry callback to a fixed list of sinks. Each sink is invoked
/// independently with its own try/catch so one sink's failure never blocks, or breaks,
/// the others (and never throws back into the OCPP session).
/// </summary>
public class TelemetryFanout : IChargerTelemetrySink
{
    private readonly IReadOnlyList<IChargerTelemetrySink> _sinks;
    private readonly ILogger<TelemetryFanout> _logger;

    public TelemetryFanout(IEnumerable<IChargerTelemetrySink> sinks, ILogger<TelemetryFanout> logger)
    {
        _sinks = sinks.ToList();
        _logger = logger;
    }

    public void OnConnected(string chargePointId) =>
        ForEach(s => s.OnConnected(chargePointId));

    public void OnDisconnected(string chargePointId) =>
        ForEach(s => s.OnDisconnected(chargePointId));

    public void OnBoot(string chargePointId, ChargerInfo info) =>
        ForEach(s => s.OnBoot(chargePointId, info));

    public void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode) =>
        ForEach(s => s.OnConnectorStatus(chargePointId, connectorId, status, errorCode));

    public void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt) =>
        ForEach(s => s.OnTransactionStarted(chargePointId, connectorId, transactionId, meterStartWh, idTag, startedAt));

    public void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt) =>
        ForEach(s => s.OnTransactionStopped(chargePointId, transactionId, meterStopWh, reason, stoppedAt));

    public void OnMeterValues(string chargePointId, MeterValuesRequest values) =>
        ForEach(s => s.OnMeterValues(chargePointId, values));

    private void ForEach(Action<IChargerTelemetrySink> call)
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
