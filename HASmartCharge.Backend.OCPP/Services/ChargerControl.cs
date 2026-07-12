using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Outbound charger control surface exposed to the rest of the app.
/// Deliberately tiny: availability, unlock, re-pushing config, and a charge-power
/// ceiling via SetChargingProfile. Charging start/stop is NOT here — that goes
/// through Home Assistant (plan.md §1). SetChargingProfile only caps the delivered
/// power; it does not start or stop a transaction.
/// </summary>
public interface IChargerControl
{
    Task<OcppCommandResult> SetConnectorAvailabilityAsync(string chargePointId, int connectorId, bool available, CancellationToken ct = default);
    Task<OcppCommandResult> UnlockConnectorAsync(string chargePointId, int connectorId, CancellationToken ct = default);

    /// <summary>
    /// Caps the current the charger will deliver on the connector to <paramref name="amps"/> A per
    /// phase (over <paramref name="numberPhases"/> phases) via a flat TxDefaultProfile. Applies to the
    /// current transaction (if any) and future ones. Callers convert the kW setpoint to amps.
    /// </summary>
    Task<OcppCommandResult> SetChargingCurrentLimitAsync(string chargePointId, int connectorId, double amps, int numberPhases, CancellationToken ct = default);

    Task ReconfigureAsync(string chargePointId, CancellationToken ct = default);
}

public sealed class ChargerControl : IChargerControl
{
    private readonly ICommandSender _commandSender;
    private readonly ChargerConfigurationService _configurationService;

    public ChargerControl(ICommandSender commandSender, ChargerConfigurationService configurationService)
    {
        _commandSender = commandSender;
        _configurationService = configurationService;
    }

    public Task<OcppCommandResult> SetConnectorAvailabilityAsync(string chargePointId, int connectorId, bool available, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "ChangeAvailability",
            new ChangeAvailabilityRequest { ConnectorId = connectorId, Type = available ? "Operative" : "Inoperative" }, ct);

    public Task<OcppCommandResult> UnlockConnectorAsync(string chargePointId, int connectorId, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "UnlockConnector",
            new UnlockConnectorRequest { ConnectorId = connectorId }, ct);

    public Task<OcppCommandResult> SetChargingCurrentLimitAsync(string chargePointId, int connectorId, double amps, int numberPhases, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "SetChargingProfile",
            SetChargingProfileRequest.ForFlatCurrentLimit(connectorId, amps, numberPhases), ct);

    public Task ReconfigureAsync(string chargePointId, CancellationToken ct = default) =>
        _configurationService.ConfigureChargerAsync(chargePointId, ct);
}
