using System.Text.Json;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// The single "full by deadline" charge plan: preview a schedule without saving,
/// create/replace the active plan, or cancel it.
/// </summary>
[ApiController]
[Route("api/plan")]
public class PlanController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPlanScheduleService _scheduleService;
    private readonly IChargePlanFactory _planFactory;

    public PlanController(ApplicationDbContext dbContext, IPlanScheduleService scheduleService, IChargePlanFactory planFactory)
    {
        _dbContext = dbContext;
        _scheduleService = scheduleService;
        _planFactory = planFactory;
    }

    /// <summary>The most recent plan that's still pending or active.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCurrentPlan(CancellationToken ct)
    {
        var plan = await _dbContext.ChargePlans
            .AsNoTracking()
            .Where(p => p.Status == ChargePlanStatus.Pending || p.Status == ChargePlanStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return plan is null ? NotFound() : Ok(ToDto(plan));
    }

    /// <summary>Runs the schedule calculator without persisting anything.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] DateTime? deadline, [FromQuery] int? targetSoc, CancellationToken ct)
    {
        if (deadline is null)
        {
            return BadRequest(new { error = "deadline is required" });
        }

        var deadlineUtc = EnsureUtc(deadline.Value);
        var calc = await _scheduleService.ComputeAsync(deadlineUtc, targetSoc, ct);

        return Ok(new
        {
            socPercent = calc.SocPercent,
            done = calc.Schedule.Done,
            feasible = calc.Schedule.Feasible,
            energyNeededKwh = calc.Schedule.EnergyNeededKwh,
            hoursNeeded = calc.Schedule.HoursNeeded,
            chargeDurationHours = calc.Schedule.ChargeDurationHours,
            selectedHours = calc.Schedule.SelectedHourStartsUtc.Select(EnsureUtc),
            estimatedCost = calc.Schedule.EstimatedCost,
            warning = calc.Warning
        });
    }

    /// <summary>Cancels any existing pending/active plan and creates a new active one.</summary>
    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        var deadlineUtc = EnsureUtc(request.DeadlineUtc);
        var plan = await _planFactory.CreateAsync(deadlineUtc, request.TargetSocPercent, ct);
        return Ok(ToDto(plan));
    }

    /// <summary>Cancels the active/pending/missed-deadline plan.</summary>
    [HttpDelete]
    public async Task<IActionResult> CancelPlan(CancellationToken ct)
    {
        var plan = await _dbContext.ChargePlans
            .Where(p => p.Status == ChargePlanStatus.Pending
                || p.Status == ChargePlanStatus.Active
                || p.Status == ChargePlanStatus.MissedDeadline)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (plan is null)
        {
            return NotFound();
        }

        plan.Status = ChargePlanStatus.Cancelled;
        plan.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return NoContent();
    }

    // SQLite round-trips DateTime as Kind=Unspecified, so re-mark these Utc here to
    // keep the JSON "Z" suffix consistent whether the plan is freshly created or read back.
    private static object ToDto(ChargePlan plan) => new
    {
        id = plan.Id,
        deadlineUtc = EnsureUtc(plan.DeadlineUtc),
        targetSocPercent = plan.TargetSocPercent,
        startSocPercent = plan.StartSocPercent,
        status = plan.Status.ToString(),
        estimatedEnergyKwh = plan.EstimatedEnergyKwh,
        estimatedCost = plan.EstimatedCost,
        selectedHours = (JsonSerializer.Deserialize<List<DateTime>>(plan.SelectedHoursJson) ?? [])
            .Select(EnsureUtc),
        createdAt = EnsureUtc(plan.CreatedAt),
        completedAt = plan.CompletedAt.HasValue ? EnsureUtc(plan.CompletedAt.Value) : (DateTime?)null
    };

    /// <summary>Query/body DateTimes may arrive Unspecified or Local; the calculator needs Utc.</summary>
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public record CreatePlanRequest(DateTime DeadlineUtc, int? TargetSocPercent);
}
