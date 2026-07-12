using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

public class ChargeControlService : IChargeControlService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHomeAssistantControl _haControl;

    public ChargeControlService(ApplicationDbContext dbContext, IHomeAssistantControl haControl)
    {
        _dbContext = dbContext;
        _haControl = haControl;
    }

    public async Task StartChargingAsync(CancellationToken ct = default)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        if (string.IsNullOrWhiteSpace(car.HaStartDomain) || string.IsNullOrWhiteSpace(car.HaStartService))
        {
            throw new InvalidOperationException("Car start service not configured");
        }

        await _haControl.CallServiceAsync(car.HaStartDomain, car.HaStartService, car.HaStartDataJson, ct);
    }

    public async Task StopChargingAsync(CancellationToken ct = default)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        if (string.IsNullOrWhiteSpace(car.HaStopDomain) || string.IsNullOrWhiteSpace(car.HaStopService))
        {
            throw new InvalidOperationException("Car stop service not configured");
        }

        await _haControl.CallServiceAsync(car.HaStopDomain, car.HaStopService, car.HaStopDataJson, ct);
    }
}
