using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Receives charger telemetry from the OCPP session layer.
/// Replaces the old domain-event + CQRS pipeline: the session calls these
/// directly as inbound OCPP messages arrive. The implementation owns the
/// in-memory snapshot (and, later, DB persistence + cost attribution).
/// </summary>
public interface IChargerTelemetrySink
{
    void OnConnected(string chargePointId);
    void OnDisconnected(string chargePointId);
    void OnBoot(string chargePointId, ChargerInfo info);
    void OnHeartbeat(string chargePointId);
    void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode);
    void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt);
    void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt);
    void OnMeterValues(string chargePointId, MeterValuesRequest values);
}
