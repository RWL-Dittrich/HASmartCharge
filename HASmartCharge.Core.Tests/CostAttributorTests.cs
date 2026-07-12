using HASmartCharge.Core.Costing;
using HASmartCharge.Core.Scheduling;

namespace HASmartCharge.Core.Tests;

public class CostAttributorTests
{
    private static readonly DateTime _hour10 = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    private static List<PricedHour> HourlyPrices(DateTime start, params decimal[] prices) =>
        prices.Select((p, i) => new PricedHour(start.AddHours(i), p)).ToList();

    [Fact]
    public void SingleIntervalWithinOneHour_AttributesFullyToThatHour()
    {
        var samples = new List<MeterSample>
        {
            new(_hour10.AddMinutes(10), 0.0),
            new(_hour10.AddMinutes(40), 3.0)
        };
        var prices = HourlyPrices(_hour10, 0.20m);

        var result = CostAttributor.Attribute(samples, prices);

        Assert.Equal(3.0, result.TotalKwh, 3);
        Assert.Equal(0.60m, result.TotalCost);
        var hour = Assert.Single(result.Hours);
        Assert.Equal(_hour10, hour.HourStartUtc);
        Assert.Equal(3.0, hour.EnergyKwh, 3);
        Assert.Equal(0.20m, hour.PricePerKwh);
        Assert.Equal(0.60m, hour.Cost);
    }

    [Fact]
    public void IntervalSpanningTwoHours_SplitsProportionally()
    {
        // 10:30 -> 11:30, 2 kWh delta => exactly 1 kWh in each hour.
        var samples = new List<MeterSample>
        {
            new(_hour10.AddMinutes(30), 0.0),
            new(_hour10.AddMinutes(90), 2.0)
        };
        var prices = HourlyPrices(_hour10, 0.10m, 0.30m);

        var result = CostAttributor.Attribute(samples, prices);

        Assert.Equal(2.0, result.TotalKwh, 3);
        Assert.Equal(2, result.Hours.Count);

        Assert.Equal(_hour10, result.Hours[0].HourStartUtc);
        Assert.Equal(1.0, result.Hours[0].EnergyKwh, 3);
        Assert.Equal(0.10m, result.Hours[0].PricePerKwh);
        Assert.Equal(0.10m, result.Hours[0].Cost);

        Assert.Equal(_hour10.AddHours(1), result.Hours[1].HourStartUtc);
        Assert.Equal(1.0, result.Hours[1].EnergyKwh, 3);
        Assert.Equal(0.30m, result.Hours[1].PricePerKwh);
        Assert.Equal(0.30m, result.Hours[1].Cost);

        Assert.Equal(0.40m, result.TotalCost);
    }

    [Fact]
    public void IntervalSpanningMultipleHours_SplitsProportionally()
    {
        // 10:00 -> 13:00, 6 kWh delta over exactly 3 hours => 2 kWh per hour.
        var samples = new List<MeterSample>
        {
            new(_hour10, 0.0),
            new(_hour10.AddHours(3), 6.0)
        };
        var prices = HourlyPrices(_hour10, 0.10m, 0.20m, 0.30m);

        var result = CostAttributor.Attribute(samples, prices);

        Assert.Equal(3, result.Hours.Count);
        Assert.All(result.Hours, h => Assert.Equal(2.0, h.EnergyKwh, 3));
        Assert.Equal(6.0, result.TotalKwh, 3);
        Assert.Equal(0.10m * 2 + 0.20m * 2 + 0.30m * 2, result.TotalCost);
    }

    [Fact]
    public void StartStopOnlySession_TwoSamplesAcrossThreeHours_SplitsByTime()
    {
        // A session recorded only as start/stop: 10:15 -> 12:45 (2.5h), 5 kWh delta.
        var start = _hour10.AddMinutes(15);
        var stop = _hour10.AddHours(2).AddMinutes(45);
        var samples = new List<MeterSample> { new(start, 100.0), new(stop, 105.0) };
        var prices = HourlyPrices(_hour10, 0.10m, 0.10m, 0.10m);

        var result = CostAttributor.Attribute(samples, prices);

        Assert.Equal(3, result.Hours.Count);
        Assert.Equal(5.0, result.TotalKwh, 3);
        // 45min, 60min, 45min out of 150min total => 1.5, 2.0, 1.5 kWh
        Assert.Equal(1.5, result.Hours[0].EnergyKwh, 3);
        Assert.Equal(2.0, result.Hours[1].EnergyKwh, 3);
        Assert.Equal(1.5, result.Hours[2].EnergyKwh, 3);
    }

    [Fact]
    public void DecreasingMeterReading_ClampedToZero_ContributesNothing()
    {
        var samples = new List<MeterSample>
        {
            new(_hour10, 10.0),
            new(_hour10.AddMinutes(30), 8.0) // meter went backwards
        };
        var prices = HourlyPrices(_hour10, 0.10m);

        var result = CostAttributor.Attribute(samples, prices);

        Assert.Equal(0.0, result.TotalKwh);
        Assert.Equal(0m, result.TotalCost);
        Assert.Empty(result.Hours);
    }

    [Fact]
    public void MissingPriceRow_ProducesZeroPriceBucket()
    {
        var samples = new List<MeterSample> { new(_hour10, 0.0), new(_hour10.AddMinutes(30), 4.0) };

        var result = CostAttributor.Attribute(samples, []); // no prices at all

        var hour = Assert.Single(result.Hours);
        Assert.Equal(4.0, hour.EnergyKwh, 3);
        Assert.Equal(0m, hour.PricePerKwh);
        Assert.Equal(0m, hour.Cost);
        Assert.Equal(0m, result.TotalCost);
    }

    [Fact]
    public void FewerThanTwoSamples_ReturnsEmptyResult()
    {
        var oneSample = new List<MeterSample> { new(_hour10, 5.0) };

        var result = CostAttributor.Attribute(oneSample, HourlyPrices(_hour10, 0.10m));

        Assert.Equal(0.0, result.TotalKwh);
        Assert.Equal(0m, result.TotalCost);
        Assert.Empty(result.Hours);

        var noSamples = CostAttributor.Attribute([], HourlyPrices(_hour10, 0.10m));
        Assert.Empty(noSamples.Hours);
    }

    [Fact]
    public void UnsortedSamples_AreSortedDefensively()
    {
        var sorted = new List<MeterSample> { new(_hour10, 0.0), new(_hour10.AddMinutes(30), 3.0) };
        var reversed = new List<MeterSample> { sorted[1], sorted[0] };
        var prices = HourlyPrices(_hour10, 0.10m);

        var expected = CostAttributor.Attribute(sorted, prices);
        var actual = CostAttributor.Attribute(reversed, prices);

        Assert.Equal(expected.TotalKwh, actual.TotalKwh, 3);
        Assert.Equal(expected.TotalCost, actual.TotalCost);
        Assert.Equal(expected.Hours.Select(h => h.HourStartUtc), actual.Hours.Select(h => h.HourStartUtc));
    }

    [Fact]
    public void AttributeInterval_WithinOneHour_AllEnergyToThatHour()
    {
        var buckets = CostAttributor.AttributeInterval(
            new MeterSample(_hour10.AddMinutes(10), 100.0),
            new MeterSample(_hour10.AddMinutes(40), 103.0));

        var bucket = Assert.Single(buckets);
        Assert.Equal(_hour10, bucket.HourStartUtc);
        Assert.Equal(3.0, bucket.EnergyKwh, 6);
    }

    [Fact]
    public void AttributeInterval_CrossingHourBoundary_SplitsByTime()
    {
        // 13:58 -> 14:03 (2 min in hour 13, 3 min in hour 14) of a 4 kWh delta.
        var start = new DateTime(2026, 7, 12, 13, 58, 0, DateTimeKind.Utc);
        var buckets = CostAttributor.AttributeInterval(
            new MeterSample(start, 10.0),
            new MeterSample(start.AddMinutes(5), 14.0));

        Assert.Equal(2, buckets.Count);
        Assert.Equal(4.0 * 2 / 5, buckets[0].EnergyKwh, 6); // hour 13
        Assert.Equal(4.0 * 3 / 5, buckets[1].EnergyKwh, 6); // hour 14
    }

    [Fact]
    public void AttributeInterval_ZeroOrNegativeDelta_ReturnsEmpty()
    {
        Assert.Empty(CostAttributor.AttributeInterval(
            new MeterSample(_hour10, 100.0), new MeterSample(_hour10.AddMinutes(10), 100.0)));
        Assert.Empty(CostAttributor.AttributeInterval(
            new MeterSample(_hour10, 100.0), new MeterSample(_hour10.AddMinutes(10), 99.0)));
    }

    [Fact]
    public void AttributeInterval_SummedIncrementally_MatchesBatchAttribute()
    {
        // Folding sample-by-sample (the recorder's incremental path) must equal attributing the
        // whole sample list at once.
        var samples = new List<MeterSample>
        {
            new(_hour10.AddMinutes(50), 0.0),
            new(_hour10.AddMinutes(70), 2.0),  // crosses into hour 11
            new(_hour10.AddMinutes(110), 6.0),
        };

        var incremental = new SortedDictionary<DateTime, double>();
        for (var i = 0; i < samples.Count - 1; i++)
        {
            foreach (var (hour, kwh) in CostAttributor.AttributeInterval(samples[i], samples[i + 1]))
            {
                incremental[hour] = incremental.GetValueOrDefault(hour) + kwh;
            }
        }

        var batch = CostAttributor.Attribute(samples, []);

        Assert.Equal(batch.Hours.Select(h => h.HourStartUtc), incremental.Keys);
        foreach (var h in batch.Hours)
        {
            Assert.Equal(h.EnergyKwh, incremental[h.HourStartUtc], 6);
        }
    }
}
