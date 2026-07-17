using System.Text.Json;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Creates the single active "full by deadline" <see cref="ChargePlan"/>: cancels any existing
/// pending/active plan, runs the schedule calculator, and inserts a new Active plan. Shared by
/// PlanController (manual create) and ChargeOrchestratorService (auto-arm on plug-in).
/// </summary>
public interface IChargePlanFactory
{
    /// <summary><paramref name="deadlineUtc"/> must already be UTC; <paramref name="targetSocPercent"/>
    /// falls back to CarSettings.TargetSocPercent when null.</summary>
    Task<ChargePlan> CreateAsync(DateTime deadlineUtc, int? targetSocPercent, CancellationToken ct = default);
}

public class ChargePlanFactory : IChargePlanFactory
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPlanScheduleService _scheduleService;

    public ChargePlanFactory(ApplicationDbContext dbContext, IPlanScheduleService scheduleService)
    {
        _dbContext = dbContext;
        _scheduleService = scheduleService;
    }

    public async Task<ChargePlan> CreateAsync(DateTime deadlineUtc, int? targetSocPercent, CancellationToken ct = default)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        var target = targetSocPercent ?? car.TargetSocPercent;

        var existingPlans = await _dbContext.ChargePlans
            .Where(p => p.Status == ChargePlanStatus.Pending
                || p.Status == ChargePlanStatus.Active
                || p.Status == ChargePlanStatus.MissedDeadline)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var existing in existingPlans)
        {
            existing.Status = ChargePlanStatus.Cancelled;
            existing.CompletedAt = now;
        }

        var calc = await _scheduleService.ComputeAsync(deadlineUtc, target, ct);

        var plan = new ChargePlan
        {
            DeadlineUtc = deadlineUtc,
            TargetSocPercent = target,
            StartSocPercent = calc.SocPercent.HasValue ? (int)Math.Round(calc.SocPercent.Value) : null,
            Status = ChargePlanStatus.Active,
            EstimatedEnergyKwh = calc.Schedule.EnergyNeededKwh,
            EstimatedCost = calc.Schedule.EstimatedCost,
            SelectedHoursJson = JsonSerializer.Serialize(calc.Schedule.SelectedHourStartsUtc),
            CreatedAt = now
        };

        _dbContext.ChargePlans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        return plan;
    }
}
