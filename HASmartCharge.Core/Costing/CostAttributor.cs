namespace HASmartCharge.Core.Costing;

using HASmartCharge.Core.Scheduling;

/// <summary>
/// Splits the energy delivered between consecutive meter samples across the UTC clock-hours
/// the sample interval spans, proportional to time, and prices each hour bucket.
/// Pure function, no I/O — see plan.md §6.2 for the algorithm this implements.
/// </summary>
public static class CostAttributor
{
    public static CostAttributionResult Attribute(IReadOnlyList<MeterSample> samples, IReadOnlyList<PricedHour> prices)
    {
        if (samples.Count < 2)
        {
            return new CostAttributionResult(0, 0, []);
        }

        var sorted = samples.OrderBy(s => s.TimestampUtc).ToList();
        var priceByHour = prices.ToDictionary(p => p.HourStartUtc, p => p.PricePerKwh);
        var energyByHour = new SortedDictionary<DateTime, double>();

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            var deltaKwh = Math.Max(0, b.CumulativeKwh - a.CumulativeKwh);

            if (deltaKwh <= 0 || b.TimestampUtc <= a.TimestampUtc)
            {
                continue; // zero-length or zero/negative-delta interval contributes nothing
            }

            foreach (var (hourStart, fraction) in SplitAcrossHours(a.TimestampUtc, b.TimestampUtc))
            {
                energyByHour[hourStart] = energyByHour.GetValueOrDefault(hourStart) + deltaKwh * fraction;
            }
        }

        var hours = new List<HourlyUsageResult>();
        var totalKwh = 0.0;
        var totalCost = 0m;

        foreach (var (hourStart, kwh) in energyByHour)
        {
            var price = priceByHour.GetValueOrDefault(hourStart, 0m); // no price row -> 0-price bucket; caller logs
            var cost = (decimal)kwh * price;

            hours.Add(new HourlyUsageResult(hourStart, kwh, price, cost));
            totalKwh += kwh;
            totalCost += cost;
        }

        return new CostAttributionResult(totalKwh, totalCost, hours);
    }

    /// <summary>
    /// Attributes the energy delivered between two consecutive meter samples across the UTC
    /// clock-hours the interval spans, proportional to time. Used for incremental persistence:
    /// each new sample contributes its own delta to the hour buckets without needing the whole
    /// sample history in memory. Returns empty for a zero/negative delta or zero-length interval.
    /// </summary>
    public static IReadOnlyList<(DateTime HourStartUtc, double EnergyKwh)> AttributeInterval(MeterSample from, MeterSample to)
    {
        var deltaKwh = Math.Max(0, to.CumulativeKwh - from.CumulativeKwh);
        if (deltaKwh <= 0 || to.TimestampUtc <= from.TimestampUtc)
        {
            return [];
        }

        return SplitAcrossHours(from.TimestampUtc, to.TimestampUtc)
            .Select(x => (x.HourStart, deltaKwh * x.Fraction))
            .ToList();
    }

    /// <summary>Yields (hourStart, fractionOfIntervalDuration) for each clock-hour the interval touches.</summary>
    private static IEnumerable<(DateTime HourStart, double Fraction)> SplitAcrossHours(DateTime start, DateTime end)
    {
        var totalTicks = (end - start).Ticks;
        var cursor = start;

        while (cursor < end)
        {
            var hourStart = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, DateTimeKind.Utc);
            var hourEnd = hourStart.AddHours(1);
            var segmentEnd = hourEnd < end ? hourEnd : end;

            yield return (hourStart, (double)(segmentEnd - cursor).Ticks / totalTicks);
            cursor = segmentEnd;
        }
    }
}
