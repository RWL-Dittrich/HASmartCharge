namespace HASmartCharge.Core.Scheduling;

/// <summary>One weekday's recurring departure. <paramref name="TargetSocPercent"/> null = use the car default.</summary>
public record WeeklyDeparture(DayOfWeek Day, bool Enabled, TimeOnly Departure, int? TargetSocPercent = null);

/// <summary>A one-off departure for a specific local date, overriding the weekly pattern. Null target = car default.</summary>
public record DepartureOverride(DateOnly Date, TimeOnly Departure, int? TargetSocPercent = null);

/// <summary>The resolved next departure: when to be full (UTC) and to what SoC (null = car default).</summary>
public record NextDeparture(DateTime DeadlineUtc, int? TargetSocPercent);

/// <summary>
/// Resolves the next departure from a recurring weekly pattern plus one-off date overrides. Pure,
/// no I/O. For each upcoming local date the applicable departure is: the override for that date if
/// one exists, else the weekly entry for that weekday if enabled. Departure times are local
/// wall-clock in <paramref name="tz"/>; the first candidate strictly after <paramref name="nowUtc"/>
/// wins. Returns null if nothing is enabled within the horizon.
/// </summary>
public static class DepartureResolver
{
    /// <summary>How many days ahead to scan for the next enabled departure.</summary>
    private const int HorizonDays = 8;

    public static NextDeparture? ResolveNextDeparture(
        DateTime nowUtc,
        TimeZoneInfo tz,
        IReadOnlyList<WeeklyDeparture> weekly,
        IReadOnlyList<DepartureOverride> overrides)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var startDate = DateOnly.FromDateTime(nowLocal);

        for (var offset = 0; offset < HorizonDays; offset++)
        {
            var date = startDate.AddDays(offset);

            var match = FindDeparture(date, weekly, overrides);
            if (match is null)
            {
                continue;
            }

            var candidateLocal = DateTime.SpecifyKind(date.ToDateTime(match.Value.Departure), DateTimeKind.Unspecified);
            var candidateUtc = TimeZoneInfo.ConvertTimeToUtc(candidateLocal, tz);

            if (candidateUtc > nowUtc)
            {
                return new NextDeparture(candidateUtc, match.Value.TargetSocPercent);
            }
        }

        return null;
    }

    /// <summary>Override for the date wins; otherwise the weekly entry for that weekday if enabled.</summary>
    private static (TimeOnly Departure, int? TargetSocPercent)? FindDeparture(
        DateOnly date, IReadOnlyList<WeeklyDeparture> weekly, IReadOnlyList<DepartureOverride> overrides)
    {
        foreach (var o in overrides)
        {
            if (o.Date == date)
            {
                return (o.Departure, o.TargetSocPercent);
            }
        }

        foreach (var w in weekly)
        {
            if (w.Day == date.DayOfWeek)
            {
                return w.Enabled ? (w.Departure, w.TargetSocPercent) : null;
            }
        }

        return null;
    }
}
