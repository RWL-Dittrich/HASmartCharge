using HASmartCharge.Core.Scheduling;

namespace HASmartCharge.Core.Tests;

public class DepartureResolverTests
{
    // Europe/Amsterdam: UTC+2 in summer (DST), UTC+1 in winter. Resolved the same way the
    // backend does (IANA id, Windows fallback) so these tests run on Linux and Windows alike.
    private static readonly TimeZoneInfo _amsterdam = ResolveAmsterdam();

    // 2026-07-16 is a Thursday; 2026-01-15 is a Thursday.
    private static readonly TimeOnly _seven = new(7, 0);

    private static List<WeeklyDeparture> Weekdays0700(IEnumerable<DayOfWeek> enabledDays, int? target = null)
    {
        var enabled = new HashSet<DayOfWeek>(enabledDays);
        return Enum.GetValues<DayOfWeek>()
            .Select(d => new WeeklyDeparture(d, enabled.Contains(d), _seven, target))
            .ToList();
    }

    private static readonly DayOfWeek[] _monToFri =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Fact]
    public void PlugInBeforeTodaysDeparture_UsesToday()
    {
        // Thursday 04:00Z = 06:00 local (summer); Thursday 07:00 local = 05:00Z is still ahead.
        var now = new DateTime(2026, 7, 16, 4, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), []);

        Assert.Equal(new DateTime(2026, 7, 16, 5, 0, 0, DateTimeKind.Utc), result?.DeadlineUtc);
    }

    [Fact]
    public void PlugInAfterTodaysDeparture_RollsToNextEnabledDay()
    {
        // Thursday 18:00Z = 20:00 local; today's 07:00 has passed → next is Friday 07:00 local = 05:00Z.
        var now = new DateTime(2026, 7, 16, 18, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), []);

        Assert.Equal(new DateTime(2026, 7, 17, 5, 0, 0, DateTimeKind.Utc), result?.DeadlineUtc);
    }

    [Fact]
    public void DisabledDays_AreSkipped()
    {
        // Friday 20:00Z = 22:00 local; Sat/Sun disabled → next is Monday 2026-07-20 07:00 local = 05:00Z.
        var now = new DateTime(2026, 7, 17, 20, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), []);

        Assert.Equal(new DateTime(2026, 7, 20, 5, 0, 0, DateTimeKind.Utc), result?.DeadlineUtc);
    }

    [Fact]
    public void Override_ReplacesWeeklyForThatDate()
    {
        // Thursday 18:00Z; Friday overridden to 20:00 local (day off) = 18:00Z, beating the 07:00 weekly.
        var now = new DateTime(2026, 7, 16, 18, 0, 0, DateTimeKind.Utc);
        var overrides = new List<DepartureOverride> { new(new DateOnly(2026, 7, 17), new TimeOnly(20, 0)) };

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), overrides);

        Assert.Equal(new DateTime(2026, 7, 17, 18, 0, 0, DateTimeKind.Utc), result?.DeadlineUtc);
    }

    [Fact]
    public void WinterDeparture_UsesStandardTimeOffset()
    {
        // Thursday 04:00Z = 05:00 local (winter, UTC+1); 07:00 local = 06:00Z.
        var now = new DateTime(2026, 1, 15, 4, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), []);

        Assert.Equal(new DateTime(2026, 1, 15, 6, 0, 0, DateTimeKind.Utc), result?.DeadlineUtc);
    }

    [Fact]
    public void NothingEnabled_ReturnsNull()
    {
        var now = new DateTime(2026, 7, 16, 18, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700([]), []);

        Assert.Null(result);
    }

    [Fact]
    public void TargetSoc_FlowsFromMatchedDay()
    {
        // Weekly days carry 80%; the Friday override carries 60% — the resolved day's target wins.
        var now = new DateTime(2026, 7, 16, 18, 0, 0, DateTimeKind.Utc);
        var weekly = Weekdays0700(_monToFri, target: 80);

        var weeklyResult = DepartureResolver.ResolveNextDeparture(now, _amsterdam, weekly, []);
        Assert.Equal(80, weeklyResult?.TargetSocPercent);

        var overrides = new List<DepartureOverride> { new(new DateOnly(2026, 7, 17), new TimeOnly(20, 0), 60) };
        var overrideResult = DepartureResolver.ResolveNextDeparture(now, _amsterdam, weekly, overrides);
        Assert.Equal(60, overrideResult?.TargetSocPercent);
    }

    [Fact]
    public void TargetSoc_NullWhenUnset()
    {
        var now = new DateTime(2026, 7, 16, 4, 0, 0, DateTimeKind.Utc);

        var result = DepartureResolver.ResolveNextDeparture(now, _amsterdam, Weekdays0700(_monToFri), []);

        Assert.Null(result?.TargetSocPercent);
    }

    private static TimeZoneInfo ResolveAmsterdam()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
