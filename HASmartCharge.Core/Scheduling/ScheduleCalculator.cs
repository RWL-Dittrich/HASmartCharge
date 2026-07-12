namespace HASmartCharge.Core.Scheduling;

/// <summary>
/// Picks the cheapest hours before a deadline to reach a target state of charge.
/// Pure function, no I/O — see plan.md §6.1 for the algorithm this implements.
/// </summary>
public static class ScheduleCalculator
{
    public static ScheduleResult Calculate(ScheduleRequest request)
    {
        var efficiency = request.ChargeEfficiency <= 0 ? 1.0 : request.ChargeEfficiency;
        var socDeltaPercent = request.TargetSocPercent - request.CurrentSocPercent;
        var energyNeededKwh = Math.Max(0, socDeltaPercent / 100.0 * request.BatteryCapacityKwh) / efficiency;

        if (energyNeededKwh == 0)
        {
            return new ScheduleResult(
                Done: true,
                Feasible: true,
                EnergyNeededKwh: 0,
                HoursNeeded: 0,
                SelectedHourStartsUtc: [],
                EstimatedCost: 0m);
        }

        if (request.MaxChargeKw <= 0)
        {
            return new ScheduleResult(
                Done: false,
                Feasible: false,
                EnergyNeededKwh: energyNeededKwh,
                HoursNeeded: 0,
                SelectedHourStartsUtc: [],
                EstimatedCost: 0m);
        }

        var hoursNeeded = (int)Math.Ceiling(energyNeededKwh / request.MaxChargeKw);

        var candidates = request.Prices
            .Where(h => h.HourStartUtc.AddHours(1) > request.NowUtc && h.HourStartUtc < request.DeadlineUtc)
            .OrderBy(h => h.PricePerKwh)
            .ThenBy(h => h.HourStartUtc)
            .ToList();

        var feasible = candidates.Count >= hoursNeeded;
        var takeCount = feasible ? hoursNeeded : candidates.Count;

        // Ascending-by-price order: the last taken element is the most expensive of the selection.
        var selected = candidates.Take(takeCount).ToList();

        var totalEnergyKwh = feasible
            ? energyNeededKwh
            : Math.Min(selected.Count * request.MaxChargeKw, energyNeededKwh);

        var estimatedCost = CalculateCost(selected, totalEnergyKwh, request.MaxChargeKw);

        var selectedHourStartsUtc = selected
            .Select(h => h.HourStartUtc)
            .OrderBy(h => h)
            .ToList();

        return new ScheduleResult(
            Done: false,
            Feasible: feasible,
            EnergyNeededKwh: energyNeededKwh,
            HoursNeeded: hoursNeeded,
            SelectedHourStartsUtc: selectedHourStartsUtc,
            EstimatedCost: estimatedCost)
        {
            ChargeDurationHours = energyNeededKwh / request.MaxChargeKw
        };
    }

    /// <summary>
    /// Full power (<paramref name="maxChargeKw"/>) in every selected hour except the most
    /// expensive one, which absorbs the remainder needed to reach <paramref name="totalEnergyKwh"/>.
    /// <paramref name="selected"/> must be ordered by price ascending — its last element is
    /// therefore the most expensive of the selection.
    /// </summary>
    private static decimal CalculateCost(IReadOnlyList<PricedHour> selected, double totalEnergyKwh, double maxChargeKw)
    {
        if (selected.Count == 0)
        {
            return 0m;
        }

        var fullHoursCount = selected.Count - 1;
        var remainderKwh = Math.Max(0, totalEnergyKwh - fullHoursCount * maxChargeKw);

        var cost = 0m;
        for (var i = 0; i < selected.Count; i++)
        {
            var isMostExpensive = i == selected.Count - 1;
            var kwh = isMostExpensive ? remainderKwh : maxChargeKw;
            cost += (decimal)kwh * selected[i].PricePerKwh;
        }

        return cost;
    }
}
