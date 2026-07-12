using System.Globalization;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services;
using HASmartCharge.Core.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Recurring auto-charge configuration: a weekly departure-time pattern plus one-off date
/// overrides. When enabled, plugging in the car auto-creates a charge plan for the next departure.
/// </summary>
[ApiController]
[Route("api/auto-schedule")]
public class AutoScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAutoScheduleResolver _resolver;

    public AutoScheduleController(ApplicationDbContext db, IAutoScheduleResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _db.AutoScheduleSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            return NotFound();
        }

        var overrides = await LoadOverridesAsync(ct);
        var next = await _resolver.ResolveNextDepartureAsync(DateTime.UtcNow, ct);
        return Ok(ToDto(settings, overrides, next));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAutoScheduleRequest request, CancellationToken ct)
    {
        var settings = await _db.AutoScheduleSettings.FirstAsync(ct);
        settings.Enabled = request.Enabled;
        settings.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? settings.TimeZoneId : request.TimeZoneId;
        settings.WeeklyJson = AutoScheduleWeekly.Serialize(request.Weekly ?? []);
        await _db.SaveChangesAsync(ct);

        var overrides = await LoadOverridesAsync(ct);
        var next = await _resolver.ResolveNextDepartureAsync(DateTime.UtcNow, ct);
        return Ok(ToDto(settings, overrides, next));
    }

    /// <summary>Adds or replaces the override for a given local date.</summary>
    [HttpPost("overrides")]
    public async Task<IActionResult> UpsertOverride([FromBody] OverrideRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.DateLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return BadRequest(new { error = "dateLocal must be yyyy-MM-dd" });
        }

        if (!TimeOnly.TryParse(request.DepartureLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return BadRequest(new { error = "departureLocal must be HH:mm" });
        }

        var existing = await _db.ScheduleOverrides.FirstOrDefaultAsync(o => o.DateLocal == date, ct);
        if (existing is null)
        {
            existing = new ScheduleOverride { DateLocal = date, CreatedAt = DateTime.UtcNow };
            _db.ScheduleOverrides.Add(existing);
        }

        existing.DepartureLocal = time;
        existing.TargetSocPercent = request.TargetSocPercent;
        await _db.SaveChangesAsync(ct);

        return Ok(ToOverrideDto(existing));
    }

    [HttpDelete("overrides/{id:int}")]
    public async Task<IActionResult> DeleteOverride(int id, CancellationToken ct)
    {
        var existing = await _db.ScheduleOverrides.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        _db.ScheduleOverrides.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<List<ScheduleOverride>> LoadOverridesAsync(CancellationToken ct) =>
        _db.ScheduleOverrides.AsNoTracking().OrderBy(o => o.DateLocal).ToListAsync(ct);

    private static object ToDto(AutoScheduleSettings s, List<ScheduleOverride> overrides, NextDeparture? next) => new
    {
        enabled = s.Enabled,
        timeZoneId = s.TimeZoneId,
        weekly = AutoScheduleWeekly.Parse(s.WeeklyJson),
        overrides = overrides.Select(ToOverrideDto),
        // ConvertTimeToUtc already returns Kind=Utc; re-stamp defensively so JSON carries the "Z".
        nextDepartureUtc = next is { } n ? DateTime.SpecifyKind(n.DeadlineUtc, DateTimeKind.Utc) : (DateTime?)null,
        nextTargetSocPercent = next?.TargetSocPercent
    };

    private static object ToOverrideDto(ScheduleOverride o) => new
    {
        id = o.Id,
        dateLocal = o.DateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        departureLocal = o.DepartureLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
        targetSocPercent = o.TargetSocPercent
    };

    public record UpdateAutoScheduleRequest(bool Enabled, string TimeZoneId, List<AutoScheduleWeekly.WeeklyEntry> Weekly);

    public record OverrideRequest(string DateLocal, string DepartureLocal, int? TargetSocPercent);
}
