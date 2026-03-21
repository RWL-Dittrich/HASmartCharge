using System.Globalization;

namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for a single measurement value and metadata.
/// </summary>
public sealed record MeasurementValueSnapshot
{
    public required string Value { get; init; }
    public string? Unit { get; init; }
    public string? Context { get; init; }
    public string? Format { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime Timestamp { get; init; }

    public decimal? AsDecimal()
    {
        return decimal.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : null;
    }

    public int? AsInt()
    {
        return int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;
    }
}
