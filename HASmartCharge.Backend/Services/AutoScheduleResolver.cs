using System.Text.Json;
using HASmartCharge.Backend.DB;
using HASmartCharge.Core.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Reads the recurring weekly pattern + one-off overrides from the DB and resolves the next
/// departure deadline (UTC) via <see cref="DepartureResolver"/>. Does NOT gate on the master
/// Enabled flag — it always answers "when is the next departure?", so the Schedule page can
/// preview it even while auto-scheduling is off. Shared by AutoScheduleController and the orchestrator.
/// </summary>
public interface IAutoScheduleResolver
{
    Task<NextDeparture?> ResolveNextDepartureAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Deletes overrides whose local date is already in the past. Returns rows removed.</summary>
    Task<int> SweepPastOverridesAsync(DateTime nowUtc, CancellationToken ct = default);
}

public class AutoScheduleResolver : IAutoScheduleResolver
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AutoScheduleResolver> _logger;

    public AutoScheduleResolver(ApplicationDbContext db, ILogger<AutoScheduleResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<NextDeparture?> ResolveNextDepartureAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var settings = await _db.AutoScheduleSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            return null;
        }

        var weekly = AutoScheduleWeekly.Parse(settings.WeeklyJson)
            .Select(e => new WeeklyDeparture(
                (DayOfWeek)e.DayOfWeek,
                e.Enabled,
                TimeOnly.TryParse(e.DepartureLocal, out var t) ? t : new TimeOnly(7, 0),
                e.TargetSocPercent))
            .ToList();

        var overrideRows = await _db.ScheduleOverrides.AsNoTracking().ToListAsync(ct);
        var overrides = overrideRows
            .Select(o => new DepartureOverride(o.DateLocal, o.DepartureLocal, o.TargetSocPercent))
            .ToList();

        var tz = ResolveTimeZone(settings.TimeZoneId);
        return DepartureResolver.ResolveNextDeparture(nowUtc, tz, weekly, overrides);
    }

    public async Task<int> SweepPastOverridesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var settings = await _db.AutoScheduleSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var tz = settings is null ? TimeZoneInfo.Utc : ResolveTimeZone(settings.TimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));

        return await _db.ScheduleOverrides
            .Where(o => o.DateLocal < todayLocal)
            .ExecuteDeleteAsync(ct);
    }

    private TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex, "Auto-schedule: time zone '{Id}' not found; falling back to UTC.", id);
            return TimeZoneInfo.Utc;
        }
    }
}

/// <summary>(De)serializes the weekly departure pattern stored on AutoScheduleSettings.WeeklyJson.</summary>
public static class AutoScheduleWeekly
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>One weekday entry as stored/exposed over the API (camelCase JSON). Null target = car default.</summary>
    public record WeeklyEntry(int DayOfWeek, bool Enabled, string DepartureLocal, int? TargetSocPercent = null);

    public static List<WeeklyEntry> Parse(string json) =>
        JsonSerializer.Deserialize<List<WeeklyEntry>>(json, _json) ?? [];

    public static string Serialize(IEnumerable<WeeklyEntry> entries) =>
        JsonSerializer.Serialize(entries.ToList(), _json);
}
