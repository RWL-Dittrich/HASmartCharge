namespace HASmartCharge.Core.Scheduling;

/// <summary>Inputs to <see cref="ScheduleCalculator.Calculate"/>.</summary>
public record ScheduleRequest(
    double CurrentSocPercent,
    int TargetSocPercent,
    double BatteryCapacityKwh,
    double ChargeEfficiency,
    double MaxChargeKw,
    DateTime NowUtc,
    DateTime DeadlineUtc,
    IReadOnlyList<PricedHour> Prices);
