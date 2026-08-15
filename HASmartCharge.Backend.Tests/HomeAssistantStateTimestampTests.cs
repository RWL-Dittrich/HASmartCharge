using System.Text.Json;
using HASmartCharge.Backend.HomeAssistant.Services;

namespace HASmartCharge.Backend.Tests;

public class HomeAssistantStateTimestampTests
{
    private static DateTimeOffset? ReadReportedAt(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return HomeAssistantControl.ReadReportedAt(doc.RootElement);
    }

    [Fact]
    public void PrefersLastReported_OverTheChangeTimestamps()
    {
        // The value has sat at 64% for an hour but the car reported it a minute ago: fresh, not stale.
        var reportedAt = ReadReportedAt("""
        {
          "state": "64",
          "last_changed": "2026-08-15T11:00:00.000000+00:00",
          "last_updated": "2026-08-15T11:00:00.000000+00:00",
          "last_reported": "2026-08-15T11:59:00.000000+00:00"
        }
        """);

        Assert.Equal(new DateTimeOffset(2026, 8, 15, 11, 59, 0, TimeSpan.Zero), reportedAt);
    }

    [Fact]
    public void FallsBackToLastUpdated_ThenLastChanged_OnOlderHomeAssistant()
    {
        // last_reported only exists on HA 2024.6+.
        Assert.Equal(
            new DateTimeOffset(2026, 8, 15, 11, 30, 0, TimeSpan.Zero),
            ReadReportedAt("""
            {"state":"64","last_changed":"2026-08-15T11:00:00+00:00","last_updated":"2026-08-15T11:30:00+00:00"}
            """));

        Assert.Equal(
            new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.Zero),
            ReadReportedAt("""{"state":"64","last_changed":"2026-08-15T11:00:00+00:00"}"""));
    }

    [Fact]
    public void ReturnsNull_WhenNoUsableTimestamp()
    {
        Assert.Null(ReadReportedAt("""{"state":"64"}"""));
        Assert.Null(ReadReportedAt("""{"state":"64","last_reported":"not a date"}"""));
        Assert.Null(ReadReportedAt("""{"state":"64","last_reported":null}"""));
    }

    [Fact]
    public void KeepsTheOffset_SoAgeIsNotShiftedByLocalTime()
    {
        var reportedAt = ReadReportedAt("""{"state":"64","last_reported":"2026-08-15T13:59:00+02:00"}""");

        Assert.Equal(new DateTimeOffset(2026, 8, 15, 11, 59, 0, TimeSpan.Zero), reportedAt!.Value.ToUniversalTime());
    }
}
