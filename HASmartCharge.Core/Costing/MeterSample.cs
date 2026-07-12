namespace HASmartCharge.Core.Costing;

/// <summary>A cumulative meter reading at a point in time, fed into <see cref="CostAttributor"/>.</summary>
public record MeterSample(DateTime TimestampUtc, double CumulativeKwh);
