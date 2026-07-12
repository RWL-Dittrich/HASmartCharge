namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Recurring auto-charge configuration. Single row (Id = 1). When <see cref="Enabled"/>, plugging
/// in the car auto-creates a charge plan whose deadline is the next departure time. The weekly
/// pattern is stored as JSON (see <see cref="WeeklyJson"/>); one-off exceptions live in
/// <see cref="ScheduleOverride"/>.
/// </summary>
public class AutoScheduleSettings
{
    public int Id { get; set; }

    /// <summary>Master switch: when true, a plug-in event auto-arms a charge plan.</summary>
    public bool Enabled { get; set; }

    /// <summary>IANA time zone the departure times are expressed in.</summary>
    public string TimeZoneId { get; set; } = "Europe/Amsterdam";

    /// <summary>
    /// JSON array of 7 entries { "dayOfWeek": 0-6, "enabled": bool, "departureLocal": "HH:mm" };
    /// dayOfWeek matches System.DayOfWeek (0 = Sunday). Seeded all-disabled so nothing fires
    /// until the user configures it.
    /// </summary>
    public string WeeklyJson { get; set; } = DefaultWeeklyJson;

    public const string DefaultWeeklyJson =
        """[{"dayOfWeek":0,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":1,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":2,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":3,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":4,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":5,"enabled":false,"departureLocal":"07:00"},{"dayOfWeek":6,"enabled":false,"departureLocal":"07:00"}]""";
}
