using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using HASmartCharge.Core.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Scoped: reads CarSettings/ChargerSettings/HourlyPrices via the request-scoped
/// ApplicationDbContext, and current SoC via IHomeAssistantControl.
/// </summary>
public class PlanScheduleService : IPlanScheduleService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHomeAssistantControl _haControl;

    public PlanScheduleService(ApplicationDbContext dbContext, IHomeAssistantControl haControl)
    {
        _dbContext = dbContext;
        _haControl = haControl;
    }

    public Task<PlanCalculation> ComputeAsync(DateTime deadlineUtc, int? targetSocPercent, CancellationToken ct = default) =>
        ComputeAsync(deadlineUtc, targetSocPercent, knownSocPercent: null, ct);

    public Task<PlanCalculation> ComputeAsync(DateTime deadlineUtc, int? targetSocPercent, double knownSocPercent, CancellationToken ct = default) =>
        ComputeAsync(deadlineUtc, targetSocPercent, (double?)knownSocPercent, ct);

    private async Task<PlanCalculation> ComputeAsync(DateTime deadlineUtc, int? targetSocPercent, double? knownSocPercent, CancellationToken ct)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);
        var nowUtc = DateTime.UtcNow;

        var socPercent = knownSocPercent;
        if (socPercent is null && !string.IsNullOrWhiteSpace(car.HaSocEntityId))
        {
            socPercent = await _haControl.GetBatterySocAsync(car.HaSocEntityId, ct);
        }

        var warning = socPercent is null ? "Battery SoC unavailable" : null;

        // Wide enough to cover the calculator's own "current hour still open" filter
        // (HourStartUtc + 1h > nowUtc), without dragging in the whole price history.
        var prices = await _dbContext.HourlyPrices
            .AsNoTracking()
            .Where(p => p.HourStartUtc < deadlineUtc && p.HourStartUtc >= nowUtc.AddHours(-1))
            .OrderBy(p => p.HourStartUtc)
            .Select(p => new PricedHour(p.HourStartUtc, p.PricePerKwh))
            .ToListAsync(ct);

        var request = new ScheduleRequest(
            CurrentSocPercent: socPercent ?? 0,
            TargetSocPercent: targetSocPercent ?? car.TargetSocPercent,
            BatteryCapacityKwh: car.BatteryCapacityKwh,
            ChargeEfficiency: car.ChargeEfficiency,
            MaxChargeKw: charger.MaxChargeKw,
            NowUtc: nowUtc,
            DeadlineUtc: deadlineUtc,
            Prices: prices);

        var schedule = ScheduleCalculator.Calculate(request);

        return new PlanCalculation(socPercent, warning, schedule);
    }
}
