using HASmartCharge.Application.Interfaces;
using HASmartCharge.Domain.Entities;

namespace HASmartCharge.Application.Commands;

public sealed class RegisterChargerHandler
{
    private readonly IChargerRepository _chargers;

    public RegisterChargerHandler(IChargerRepository chargers)
    {
        _chargers = chargers;
    }

    public async Task HandleAsync(RegisterChargerCommand command, CancellationToken ct = default)
    {
        Charger? charger = await _chargers.GetByIdAsync(command.ChargePointId, ct);
        if (charger is null)
        {
            charger = Charger.Register(command.ChargePointId, command.Vendor, command.Model, command.SerialNumber, command.FirmwareVersion);
        }
        else
        {
            charger.UpdateHardwareInfo(command.Vendor, command.Model, command.SerialNumber, command.FirmwareVersion);
        }
        await _chargers.SaveAsync(charger, ct);
    }
}
