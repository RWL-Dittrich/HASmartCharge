namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// A one-off departure time for a specific local date, overriding the weekly pattern
/// (e.g. a day off → leave later so the car charges cheaply during the day). One row per date.
/// </summary>
public class ScheduleOverride
{
    public int Id { get; set; }

    /// <summary>Local calendar date this override applies to.</summary>
    public DateOnly DateLocal { get; set; }

    /// <summary>Local wall-clock departure time for that date.</summary>
    public TimeOnly DepartureLocal { get; set; }

    /// <summary>Target battery % for that date; null falls back to CarSettings.TargetSocPercent.</summary>
    public int? TargetSocPercent { get; set; }

    public DateTime CreatedAt { get; set; }
}
