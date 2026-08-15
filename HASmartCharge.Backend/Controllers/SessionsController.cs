using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Core.Calibration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Charge session history: kWh + cost per session, with an hourly cost breakdown per session.
/// </summary>
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public SessionsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>All sessions, newest-first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var sessions = await _dbContext.ChargeSessions
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(ct);

        var batteryKwh = await BatteryCapacityKwhAsync(ct);

        return Ok(sessions.Select(s => ToSummaryDto(s, batteryKwh)));
    }

    /// <summary>Session detail including the per-hour cost breakdown.</summary>
    [HttpGet("{transactionId:int}")]
    public async Task<IActionResult> GetSession(int transactionId, CancellationToken ct)
    {
        var session = await _dbContext.ChargeSessions
            .AsNoTracking()
            .Include(s => s.HourlyUsage)
            .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);

        if (session is null)
        {
            return NotFound();
        }

        var efficiency = Efficiency(session, await BatteryCapacityKwhAsync(ct));

        return Ok(new
        {
            transactionId = session.TransactionId,
            chargePointId = session.ChargePointId,
            connectorId = session.ConnectorId,
            startedAt = EnsureUtc(session.StartedAt),
            completedAt = session.CompletedAt.HasValue ? EnsureUtc(session.CompletedAt.Value) : (DateTime?)null,
            totalKwh = session.TotalKwh,
            totalCost = session.TotalCost,
            avgPricePerKwh = AveragePrice(session),
            planId = session.PlanId,
            startSocPercent = session.StartSocPercent,
            endSocPercent = session.EndSocPercent,
            efficiency = efficiency.Value,
            efficiencyCounted = efficiency.Counted,
            hourlyBreakdown = session.HourlyUsage
                .OrderBy(u => u.HourStartUtc)
                .Select(u => new
                {
                    hourStartUtc = EnsureUtc(u.HourStartUtc),
                    energyKwh = u.EnergyKwh,
                    pricePerKwh = u.PricePerKwh,
                    cost = u.Cost
                })
        });
    }

    /// <summary>Delete a single session (cascades its hourly usage rows).</summary>
    [HttpDelete("{transactionId:int}")]
    public async Task<IActionResult> DeleteSession(int transactionId, CancellationToken ct)
    {
        var session = await _dbContext.ChargeSessions
            .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);

        if (session is null)
        {
            return NotFound();
        }

        _dbContext.ChargeSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    private static object ToSummaryDto(ChargeSession session, double batteryCapacityKwh)
    {
        var efficiency = Efficiency(session, batteryCapacityKwh);

        return new
        {
            transactionId = session.TransactionId,
            chargePointId = session.ChargePointId,
            connectorId = session.ConnectorId,
            startedAt = EnsureUtc(session.StartedAt),
            completedAt = session.CompletedAt.HasValue ? EnsureUtc(session.CompletedAt.Value) : (DateTime?)null,
            totalKwh = session.TotalKwh,
            totalCost = session.TotalCost,
            avgPricePerKwh = AveragePrice(session),
            planId = session.PlanId,
            startSocPercent = session.StartSocPercent,
            endSocPercent = session.EndSocPercent,
            efficiency = efficiency.Value,
            efficiencyCounted = efficiency.Counted
        };
    }

    private Task<double> BatteryCapacityKwhAsync(CancellationToken ct) =>
        _dbContext.CarSettings.AsNoTracking().Select(c => c.BatteryCapacityKwh).FirstAsync(ct);

    /// <summary>
    /// This session's own grid → battery efficiency, plus whether it was clean enough to feed the
    /// settings-page estimate — a session below the noise thresholds still shows its number, just
    /// marked as not counted (see <see cref="EfficiencyEstimator"/>).
    /// </summary>
    private static (double? Value, bool Counted) Efficiency(ChargeSession session, double batteryCapacityKwh)
    {
        if (session.StartSocPercent is not { } startSoc || session.EndSocPercent is not { } endSoc)
        {
            return (null, false);
        }

        var sample = new EfficiencySample(startSoc, endSoc, session.TotalKwh);
        return (EfficiencyEstimator.Ratio(sample, batteryCapacityKwh), EfficiencyEstimator.IsUsable(sample));
    }

    private static decimal? AveragePrice(ChargeSession session) =>
        session.TotalKwh > 0 ? session.TotalCost / (decimal)session.TotalKwh : null;

    // SQLite round-trips DateTime as Kind=Unspecified, so re-mark these Utc here to
    // keep the JSON "Z" suffix consistent (see PlanController.EnsureUtc for the same pattern).
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
