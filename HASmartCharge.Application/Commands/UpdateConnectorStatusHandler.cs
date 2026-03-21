using HASmartCharge.Application.Interfaces;
using HASmartCharge.Domain.Entities;

namespace HASmartCharge.Application.Commands;

public sealed class UpdateConnectorStatusHandler
{
    private readonly IChargerRepository _chargers;

    public UpdateConnectorStatusHandler(IChargerRepository chargers)
    {
        _chargers = chargers;
    }

    public async Task HandleAsync(UpdateConnectorStatusCommand command, CancellationToken ct = default)
    {
        Charger? charger = await _chargers.GetByIdAsync(command.ChargePointId, ct);
        if (charger is null) return;
        charger.AddOrUpdateConnector(command.ConnectorId, command.Status, command.ErrorCode);
        await _chargers.SaveAsync(charger, ct);
    }
}
