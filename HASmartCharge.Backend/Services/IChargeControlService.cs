namespace HASmartCharge.Backend.Services;

/// <summary>
/// Single choke point for starting/stopping charging, branching on <c>CarSettings.ChargeControlMode</c>:
/// the default <c>HomeAssistant</c> mode calls the HA start/stop services configured on the car;
/// <c>Charger</c> mode drives the charger itself instead — OCPP RemoteStartTransaction/RemoteStopTransaction
/// for an OCPP charger, or Zaptec pause/resume (506/507) for a Zaptec charger. Shared by the
/// orchestrator's automatic toggling and the manual override endpoints, both of which catch exactly
/// <see cref="InvalidOperationException"/>, so every failure path (HA not configured, no OCPP charger
/// configured, no active transaction to stop, charger rejected the command, Zaptec call failed) must
/// throw that type.
/// </summary>
public interface IChargeControlService
{
    /// <summary>Throws <see cref="InvalidOperationException"/> if the configured start path isn't
    /// configured, is disconnected, or the charger rejects the command. In Zaptec mode, a session the
    /// car already ended on its own or one that hasn't started yet is a silent no-op, not an error.</summary>
    Task StartChargingAsync(CancellationToken ct = default);

    /// <summary>Throws <see cref="InvalidOperationException"/> if the configured stop path isn't
    /// configured, is disconnected, has no active transaction to stop, or the charger rejects the command.</summary>
    Task StopChargingAsync(CancellationToken ct = default);
}
