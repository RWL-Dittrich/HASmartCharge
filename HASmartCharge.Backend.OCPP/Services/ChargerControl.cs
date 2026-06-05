using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Outbound charger control surface exposed to the rest of the app.
/// Deliberately tiny: availability, unlock, and re-pushing config.
/// Charging start/stop is NOT here — that goes through Home Assistant.
/// </summary>
public interface IChargerControl
{
    Task<OcppCommandResult> SetConnectorAvailabilityAsync(string chargePointId, int connectorId, bool available, CancellationToken ct = default);
    Task<OcppCommandResult> UnlockConnectorAsync(string chargePointId, int connectorId, CancellationToken ct = default);
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

    public Task ReconfigureAsync(string chargePointId, CancellationToken ct = default) =>
        _configurationService.ConfigureChargerAsync(chargePointId, ct);
}
