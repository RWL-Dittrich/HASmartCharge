namespace HASmartCharge.Core.Costing;

/// <summary>One clock-hour's share of a session's energy/cost.</summary>
public record HourlyUsageResult(DateTime HourStartUtc, double EnergyKwh, decimal PricePerKwh, decimal Cost);

/// <summary>Output of <see cref="CostAttributor.Attribute"/>.</summary>
public record CostAttributionResult(double TotalKwh, decimal TotalCost, IReadOnlyList<HourlyUsageResult> Hours);
