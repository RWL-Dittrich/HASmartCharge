namespace HASmartCharge.Core.Scheduling;

/// <summary>Output of <see cref="ScheduleCalculator.Calculate"/>.</summary>
public record ScheduleResult(
    bool Done,
    bool Feasible,
    double EnergyNeededKwh,
    int HoursNeeded,
    IReadOnlyList<DateTime> SelectedHourStartsUtc,
    decimal EstimatedCost)
{
    /// <summary>
    /// Actual charging time at full power (<c>EnergyNeededKwh / MaxChargeKw</c>), fractional.
    /// <see cref="HoursNeeded"/> is this rounded up to whole price-hour slots.
    /// </summary>
    public double ChargeDurationHours { get; init; }
}
