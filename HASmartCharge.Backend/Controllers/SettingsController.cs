using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// CRUD for the three single-row settings tables (price provider, car, charger).
/// Rows are seeded with Id = 1; PUT updates that row and ignores any incoming Id.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public SettingsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("price")]
    public async Task<ActionResult<PriceProviderSettings>> GetPriceSettings(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.PriceProviderSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut("price")]
    public async Task<ActionResult<PriceProviderSettings>> UpdatePriceSettings(
        PriceProviderSettings update, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.PriceProviderSettings.FirstAsync(cancellationToken);

        settings.ApiUrl = update.ApiUrl;
        settings.SupplierSlug = update.SupplierSlug;
        settings.Currency = update.Currency;
        settings.RefreshMinutes = update.RefreshMinutes;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("car")]
    public async Task<ActionResult<CarSettings>> GetCarSettings(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.CarSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut("car")]
    public async Task<ActionResult<CarSettings>> UpdateCarSettings(
        CarSettings update, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.CarSettings.FirstAsync(cancellationToken);

        settings.Name = update.Name;
        settings.BatteryCapacityKwh = update.BatteryCapacityKwh;
        settings.TargetSocPercent = update.TargetSocPercent;
        settings.ChargeEfficiency = update.ChargeEfficiency;
        settings.HaSocEntityId = update.HaSocEntityId;
        settings.HaStartDomain = update.HaStartDomain;
        settings.HaStartService = update.HaStartService;
        settings.HaStartDataJson = update.HaStartDataJson;
        settings.HaStopDomain = update.HaStopDomain;
        settings.HaStopService = update.HaStopService;
        settings.HaStopDataJson = update.HaStopDataJson;
        settings.HaPluggedInEntityId = update.HaPluggedInEntityId;
        settings.HaChargingStateEntityId = update.HaChargingStateEntityId;
        settings.HaTargetSocEntityId = update.HaTargetSocEntityId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("charger")]
    public async Task<ActionResult<ChargerSettings>> GetChargerSettings(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut("charger")]
    public async Task<ActionResult<ChargerSettings>> UpdateChargerSettings(
        ChargerSettings update, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ChargerSettings.FirstAsync(cancellationToken);

        settings.ChargePointId = update.ChargePointId;
        settings.FriendlyName = update.FriendlyName;
        settings.MaxChargeKw = update.MaxChargeKw;
        settings.ConnectorId = update.ConnectorId;
        // Slider bounds are editable here; ChargePowerSetpointKw is owned by POST /api/charger/power.
        settings.ChargePowerMinKw = update.ChargePowerMinKw;
        settings.ChargePowerMaxKw = update.ChargePowerMaxKw;
        settings.SupplyVoltage = update.SupplyVoltage;
        settings.PhaseCount = update.PhaseCount;
        settings.HeartbeatInterval = update.HeartbeatInterval;
        settings.MeterValueSampleInterval = update.MeterValueSampleInterval;
        settings.ClockAlignedDataInterval = update.ClockAlignedDataInterval;
        settings.MeterValuesSampledData = update.MeterValuesSampledData;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }
}
