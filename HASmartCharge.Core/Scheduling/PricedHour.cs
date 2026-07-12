namespace HASmartCharge.Core.Scheduling;

/// <summary>An hourly price point fed into the schedule calculator.</summary>
public record PricedHour(DateTime HourStartUtc, decimal PricePerKwh);
