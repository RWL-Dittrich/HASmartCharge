using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Outbound charger control surface exposed to the rest of the app.
/// Availability, unlock, re-pushing config, a charge-power ceiling via SetChargingProfile,
/// and OCPP RemoteStartTransaction/RemoteStopTransaction. The OCPP start/stop pair is used
/// ONLY when CarSettings.ChargeControlMode == "Charger" — Home Assistant remains the default
/// path for charging start/stop (plan.md §1; rule reversed 2026-08-21 by user request).
/// SetChargingProfile only caps the delivered power; it does not start or stop a transaction.
/// </summary>
public interface IChargerControl
{
    Task<OcppCommandResult> SetConnectorAvailabilityAsync(string chargePointId, int connectorId, bool available, CancellationToken ct = default);
    Task<OcppCommandResult> UnlockConnectorAsync(string chargePointId, int connectorId, CancellationToken ct = default);

    /// <summary>
    /// Sends RemoteStartTransaction with a fixed idTag ("hasmartcharge") — inbound authorization
    /// is auto-accept, so any tag works. Only used when CarSettings.ChargeControlMode == "Charger".
    /// </summary>
    Task<OcppCommandResult> RemoteStartTransactionAsync(string chargePointId, int connectorId, CancellationToken ct = default);

    /// <summary>
    /// Sends RemoteStopTransaction for an already-known transaction id. Only used when
    /// CarSettings.ChargeControlMode == "Charger".
    /// </summary>
    Task<OcppCommandResult> RemoteStopTransactionAsync(string chargePointId, int transactionId, CancellationToken ct = default);

    /// <summary>
    /// Caps the current the charger will deliver on the connector to <paramref name="amps"/> A per
    /// phase (over <paramref name="numberPhases"/> phases) via a flat TxDefaultProfile. Applies to the
    /// current transaction (if any) and future ones. Callers convert the kW setpoint to amps.
    /// </summary>
    Task<OcppCommandResult> SetChargingCurrentLimitAsync(string chargePointId, int connectorId, double amps, int numberPhases, CancellationToken ct = default);

    /// <summary>
    /// Writes a single configuration key (ChangeConfiguration). Used by chargers that cap power
    /// through a vendor key (e.g. USER_PMAX) instead of a charging profile.
    /// </summary>
    Task<OcppCommandResult> SetConfigurationKeyAsync(string chargePointId, string key, string value, CancellationToken ct = default);

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

    public Task<OcppCommandResult> RemoteStartTransactionAsync(string chargePointId, int connectorId, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "RemoteStartTransaction",
            new RemoteStartTransactionRequest { ConnectorId = connectorId, IdTag = "hasmartcharge" }, ct);

    public Task<OcppCommandResult> RemoteStopTransactionAsync(string chargePointId, int transactionId, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "RemoteStopTransaction",
            new RemoteStopTransactionRequest { TransactionId = transactionId }, ct);

    public Task<OcppCommandResult> SetChargingCurrentLimitAsync(string chargePointId, int connectorId, double amps, int numberPhases, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "SetChargingProfile",
            SetChargingProfileRequest.ForFlatCurrentLimit(connectorId, amps, numberPhases), ct);

    public Task<OcppCommandResult> SetConfigurationKeyAsync(string chargePointId, string key, string value, CancellationToken ct = default) =>
        _commandSender.SendCommandAsync(chargePointId, "ChangeConfiguration",
            new ChangeConfigurationRequest { Key = key, Value = value }, ct);

    public Task ReconfigureAsync(string chargePointId, CancellationToken ct = default) =>
        _configurationService.ConfigureChargerAsync(chargePointId, ct);
}
