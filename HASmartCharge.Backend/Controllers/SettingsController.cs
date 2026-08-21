using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services.Mqtt;
using HASmartCharge.Core.Calibration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// CRUD for the four single-row settings tables (price provider, car, charger, MQTT).
/// Rows are seeded with Id = 1; PUT updates that row and ignores any incoming Id.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMqttSettingsNotifier _mqttNotifier;

    public SettingsController(ApplicationDbContext dbContext, IMqttSettingsNotifier mqttNotifier)
    {
        _dbContext = dbContext;
        _mqttNotifier = mqttNotifier;
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
        settings.ChargeControlMode = update.ChargeControlMode;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Charge efficiency measured from real sessions (SoC gained × capacity ÷ metered kWh), so the
    /// hand-entered value can be checked against what the car actually does. Read-only: applying it
    /// is a normal PUT of the car settings.
    /// </summary>
    [HttpGet("car/efficiency-estimate")]
    public async Task<IActionResult> GetEfficiencyEstimate(CancellationToken cancellationToken)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(cancellationToken);

        // Newest first, capped: the car's real efficiency drifts (season, battery age), so an
        // unbounded history would keep dragging the estimate toward stale sessions.
        var samples = await _dbContext.ChargeSessions
            .AsNoTracking()
            .Where(s => s.CompletedAt != null && s.StartSocPercent != null && s.EndSocPercent != null)
            .OrderByDescending(s => s.StartedAt)
            .Take(30)
            .Select(s => new EfficiencySample(s.StartSocPercent!.Value, s.EndSocPercent!.Value, s.TotalKwh))
            .ToListAsync(cancellationToken);

        var estimate = EfficiencyEstimator.Estimate(samples, car.BatteryCapacityKwh);

        return Ok(new
        {
            configuredEfficiency = car.ChargeEfficiency,
            measuredEfficiency = estimate.Efficiency,
            sessionCount = estimate.SampleCount,
            candidateSessionCount = samples.Count,
            batteryKwh = estimate.BatteryKwh,
            gridKwh = estimate.GridKwh,
            plausible = EfficiencyEstimator.IsPlausible(estimate.Efficiency)
        });
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
        settings.ChargerType = update.ChargerType;
        settings.ZaptecUsername = update.ZaptecUsername;
        settings.ZaptecPassword = update.ZaptecPassword;
        settings.ZaptecChargerId = update.ZaptecChargerId;
        settings.ZaptecPollSeconds = update.ZaptecPollSeconds;
        // Slider bounds are editable here; ChargePowerSetpointKw is owned by POST /api/charger/power.
        settings.ChargePowerMinKw = update.ChargePowerMinKw;
        settings.ChargePowerMaxKw = update.ChargePowerMaxKw;
        settings.SupplyVoltage = update.SupplyVoltage;
        settings.PhaseCount = update.PhaseCount;
        settings.ChargePowerControlMode = update.ChargePowerControlMode;
        settings.ChargePowerConfigurationKey = update.ChargePowerConfigurationKey;
        settings.ChargePowerConfigurationUnit = update.ChargePowerConfigurationUnit;
        settings.HeartbeatInterval = update.HeartbeatInterval;
        settings.MeterValueSampleInterval = update.MeterValueSampleInterval;
        settings.ClockAlignedDataInterval = update.ClockAlignedDataInterval;
        settings.MeterValuesSampledData = update.MeterValuesSampledData;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("mqtt")]
    public async Task<ActionResult<MqttSettings>> GetMqttSettings(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.MqttSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut("mqtt")]
    public async Task<ActionResult<MqttSettings>> UpdateMqttSettings(
        MqttSettings update, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.MqttSettings.FirstAsync(cancellationToken);

        settings.Enabled = update.Enabled;
        settings.Host = update.Host;
        settings.Port = update.Port;
        settings.Username = update.Username;
        settings.Password = update.Password;
        settings.UseTls = update.UseTls;
        settings.ClientId = update.ClientId;
        settings.BaseTopic = update.BaseTopic;
        settings.DiscoveryPrefix = update.DiscoveryPrefix;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Wake the publisher so it reconnects/republishes immediately instead of at the next tick.
        _mqttNotifier.NotifyChanged();

        return Ok(settings);
    }
}
