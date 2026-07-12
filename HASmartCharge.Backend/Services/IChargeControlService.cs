namespace HASmartCharge.Backend.Services;

/// <summary>
/// Thin wrapper around the Home Assistant start/stop service calls for the car,
/// reading the domain/service/data from <c>CarSettings</c>. Shared by the
/// orchestrator's automatic toggling and the manual override endpoints.
/// </summary>
public interface IChargeControlService
{
    /// <summary>Throws <see cref="InvalidOperationException"/> if the start service isn't configured, or HA is disconnected.</summary>
    Task StartChargingAsync(CancellationToken ct = default);

    /// <summary>Throws <see cref="InvalidOperationException"/> if the stop service isn't configured, or HA is disconnected.</summary>
    Task StopChargingAsync(CancellationToken ct = default);
}
