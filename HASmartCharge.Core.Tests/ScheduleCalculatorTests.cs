using HASmartCharge.Core.Scheduling;

namespace HASmartCharge.Core.Tests;

public class ScheduleCalculatorTests
{
    private static readonly DateTime _now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    private static ScheduleRequest BaseRequest(
        double currentSoc = 20,
        int targetSoc = 80,
        double capacityKwh = 60,
        double efficiency = 1.0,
        double maxChargeKw = 10,
        DateTime? nowUtc = null,
        DateTime? deadlineUtc = null,
        IReadOnlyList<PricedHour>? prices = null) =>
        new(
            currentSoc,
            targetSoc,
            capacityKwh,
            efficiency,
            maxChargeKw,
            nowUtc ?? _now,
            deadlineUtc ?? _now.AddHours(24),
            prices ?? []);

    private static List<PricedHour> HourlyPrices(DateTime start, params decimal[] prices) =>
        prices.Select((p, i) => new PricedHour(start.AddHours(i), p)).ToList();

    [Fact]
    public void HappyPath_PicksCheapestHours()
    {
        // Need (80-20)/100*60 = 36 kWh at 10 kW => 4 hours.
        var prices = HourlyPrices(_now, 0.50m, 0.10m, 0.80m, 0.20m, 0.05m, 0.60m);
        var request = BaseRequest(prices: prices, deadlineUtc: _now.AddHours(6));

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Done);
        Assert.True(result.Feasible);
        Assert.Equal(36, result.EnergyNeededKwh, 3);
        Assert.Equal(4, result.HoursNeeded);

        // Cheapest 4: 0.05 (h4), 0.10 (h1), 0.20 (h3), 0.50 (h0) -> hours 0,1,3,4 chronologically.
        var expectedHours = new[] { _now, _now.AddHours(1), _now.AddHours(3), _now.AddHours(4) };
        Assert.Equal(expectedHours, result.SelectedHourStartsUtc);
    }

    [Fact]
    public void SocAlreadyAtTarget_IsDone()
    {
        var request = BaseRequest(currentSoc: 80, targetSoc: 80, prices: HourlyPrices(_now, 0.10m));

        var result = ScheduleCalculator.Calculate(request);

        Assert.True(result.Done);
        Assert.True(result.Feasible);
        Assert.Equal(0, result.EnergyNeededKwh);
        Assert.Equal(0, result.HoursNeeded);
        Assert.Empty(result.SelectedHourStartsUtc);
        Assert.Equal(0m, result.EstimatedCost);
    }

    [Fact]
    public void SocAboveTarget_IsDone()
    {
        var request = BaseRequest(currentSoc: 90, targetSoc: 80, prices: HourlyPrices(_now, 0.10m));

        var result = ScheduleCalculator.Calculate(request);

        Assert.True(result.Done);
        Assert.Equal(0, result.EnergyNeededKwh);
    }

    [Fact]
    public void DeadlineInThePast_IsNotFeasible()
    {
        var prices = HourlyPrices(_now.AddHours(-5), 0.10m, 0.10m, 0.10m, 0.10m, 0.10m);
        var request = BaseRequest(prices: prices, deadlineUtc: _now.AddHours(-1));

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Done);
        Assert.False(result.Feasible);
        Assert.Empty(result.SelectedHourStartsUtc);
    }

    [Fact]
    public void NotEnoughHours_SelectsAllCandidatesAndIsInfeasible()
    {
        // Need 36 kWh at 10 kW => 4 hours, but only 2 candidate hours available before deadline.
        var prices = HourlyPrices(_now, 0.30m, 0.10m);
        var request = BaseRequest(prices: prices, deadlineUtc: _now.AddHours(2));

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Feasible);
        Assert.Equal(4, result.HoursNeeded);
        Assert.Equal(2, result.SelectedHourStartsUtc.Count);
        Assert.Equal(new[] { _now, _now.AddHours(1) }, result.SelectedHourStartsUtc);

        // Undersupply: both hours charge at full 10 kW (20 kWh total, still under the 36 kWh need).
        var expectedCost = 10m * 0.30m + 10m * 0.10m;
        Assert.Equal(expectedCost, result.EstimatedCost);
    }

    [Fact]
    public void PartialHourRemainder_IsCostedOnMostExpensiveSelectedHour()
    {
        // Need (30-20)/100*60 = 6 kWh at 10 kW => ceil(0.6) = 1 hour, remainder = 6 kWh in that one hour.
        var prices = HourlyPrices(_now, 0.10m, 0.50m, 0.05m);
        var request = BaseRequest(currentSoc: 20, targetSoc: 30, prices: prices, deadlineUtc: _now.AddHours(3));

        var result = ScheduleCalculator.Calculate(request);

        Assert.True(result.Feasible);
        Assert.Equal(1, result.HoursNeeded);
        Assert.Equal(6, result.EnergyNeededKwh, 3);
        Assert.Equal([_now.AddHours(2)], result.SelectedHourStartsUtc); // cheapest hour: 0.05
        Assert.Equal(6m * 0.05m, result.EstimatedCost);
    }

    [Fact]
    public void PartialHourRemainder_MultiHour_CostsRemainderOnMostExpensiveSelected()
    {
        // Need (80-20)/100*60 = 36 kWh at 10 kW => ceil(3.6) = 4 hours; remainder = 36 - 3*10 = 6 kWh.
        var prices = HourlyPrices(_now, 0.10m, 0.20m, 0.30m, 0.40m, 0.50m);
        var request = BaseRequest(prices: prices, deadlineUtc: _now.AddHours(5));

        var result = ScheduleCalculator.Calculate(request);

        Assert.True(result.Feasible);
        // Cheapest 4 candidates by price: 0.10, 0.20, 0.30, 0.40 -> hours 0,1,2,3. Most expensive selected = 0.40 (hour 3).
        Assert.Equal(new[] { _now, _now.AddHours(1), _now.AddHours(2), _now.AddHours(3) }, result.SelectedHourStartsUtc);

        var expectedCost = 10m * 0.10m + 10m * 0.20m + 10m * 0.30m + 6m * 0.40m;
        Assert.Equal(expectedCost, result.EstimatedCost);
    }

    [Fact]
    public void EfficiencyScalesEnergyNeeded()
    {
        // (80-20)/100*60 = 36 kWh battery-side; at 0.9 efficiency, grid-side = 40 kWh.
        var request = BaseRequest(efficiency: 0.9, prices: HourlyPrices(_now, 0.10m));

        var result = ScheduleCalculator.Calculate(request);

        Assert.Equal(40, result.EnergyNeededKwh, 3);
    }

    [Fact]
    public void ZeroOrNegativeEfficiency_TreatedAsOne()
    {
        var requestZero = BaseRequest(efficiency: 0, prices: HourlyPrices(_now, 0.10m));
        var requestNegative = BaseRequest(efficiency: -0.5, prices: HourlyPrices(_now, 0.10m));

        var resultZero = ScheduleCalculator.Calculate(requestZero);
        var resultNegative = ScheduleCalculator.Calculate(requestNegative);

        Assert.Equal(36, resultZero.EnergyNeededKwh, 3);
        Assert.Equal(36, resultNegative.EnergyNeededKwh, 3);
    }

    [Fact]
    public void CurrentHour_PartiallyElapsed_IsStillSelectable()
    {
        // now is 10:30, the 10:00 hour ends at 11:00 -> still a valid candidate.
        var partiallyElapsedNow = new DateTime(2026, 7, 12, 10, 30, 0, DateTimeKind.Utc);
        var prices = HourlyPrices(_now, 0.05m); // hour starting at 10:00
        var request = BaseRequest(
            currentSoc: 79, targetSoc: 80, nowUtc: partiallyElapsedNow, prices: prices, deadlineUtc: _now.AddHours(2));

        var result = ScheduleCalculator.Calculate(request);

        Assert.True(result.Feasible);
        Assert.Single(result.SelectedHourStartsUtc);
        Assert.Equal(_now, result.SelectedHourStartsUtc[0]);
    }

    [Fact]
    public void EmptyPrices_IsNotFeasible()
    {
        var request = BaseRequest(prices: []);

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Done);
        Assert.False(result.Feasible);
        Assert.Empty(result.SelectedHourStartsUtc);
        Assert.Equal(0m, result.EstimatedCost);
    }

    [Fact]
    public void MaxChargeKwZero_GuardsAgainstDivideByZero()
    {
        var request = BaseRequest(maxChargeKw: 0, prices: HourlyPrices(_now, 0.10m, 0.20m));

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Done);
        Assert.False(result.Feasible);
        Assert.Equal(0, result.HoursNeeded);
        Assert.Empty(result.SelectedHourStartsUtc);
        Assert.Equal(0m, result.EstimatedCost);
    }

    [Fact]
    public void MaxChargeKwNegative_GuardsAgainstDivideByZero()
    {
        var request = BaseRequest(maxChargeKw: -5, prices: HourlyPrices(_now, 0.10m));

        var result = ScheduleCalculator.Calculate(request);

        Assert.False(result.Feasible);
        Assert.Empty(result.SelectedHourStartsUtc);
    }
}
