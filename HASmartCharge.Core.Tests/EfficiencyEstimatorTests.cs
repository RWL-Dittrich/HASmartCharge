using HASmartCharge.Core.Calibration;

namespace HASmartCharge.Core.Tests;

public class EfficiencyEstimatorTests
{
    [Fact]
    public void PoolsSessionsEnergyWeighted()
    {
        // 75 kWh battery: 20% = 15 kWh into the battery from 16.667 kWh grid = 0.90.
        var samples = new[]
        {
            new EfficiencySample(30, 50, 16.667),
            new EfficiencySample(40, 60, 16.667)
        };

        var estimate = EfficiencyEstimator.Estimate(samples, batteryCapacityKwh: 75);

        Assert.Equal(2, estimate.SampleCount);
        Assert.Equal(0.90, estimate.Efficiency!.Value, 3);
    }

    [Fact]
    public void DropsSessionsBelowNoiseThresholds()
    {
        var samples = new[]
        {
            new EfficiencySample(50, 53, 3.0),   // SoC delta too small
            new EfficiencySample(50, 60, 0.4),   // energy too small
            new EfficiencySample(60, 50, 12.0),  // SoC went down (car drove off mid-read)
            new EfficiencySample(20, 40, 22.222) // the only usable one: 15 kWh / 22.222 = 0.675
        };

        var estimate = EfficiencyEstimator.Estimate(samples, batteryCapacityKwh: 75);

        Assert.Equal(1, estimate.SampleCount);
        Assert.Equal(0.675, estimate.Efficiency!.Value, 3);
    }

    [Fact]
    public void NoUsableSessions_ReturnsNull()
    {
        var estimate = EfficiencyEstimator.Estimate([new EfficiencySample(50, 51, 0.2)], batteryCapacityKwh: 75);

        Assert.Null(estimate.Efficiency);
        Assert.Equal(0, estimate.SampleCount);
    }

    [Fact]
    public void Ratio_IsUnfiltered_SoASmallSessionStillShowsItsNumber()
    {
        // Same session the pooled estimate refuses: 3% of 75 kWh = 2.25 kWh from 3 kWh = 0.75.
        var noisy = new EfficiencySample(50, 53, 3.0);

        Assert.Equal(0.75, EfficiencyEstimator.Ratio(noisy, batteryCapacityKwh: 75)!.Value, 3);
        Assert.False(EfficiencyEstimator.IsUsable(noisy));
        Assert.Null(EfficiencyEstimator.Estimate([noisy], batteryCapacityKwh: 75).Efficiency);
    }

    [Fact]
    public void Ratio_ReturnsNull_WithoutCapacityOrMeteredEnergy()
    {
        var sample = new EfficiencySample(20, 60, 30);

        Assert.Null(EfficiencyEstimator.Ratio(sample, batteryCapacityKwh: 0));
        Assert.Null(EfficiencyEstimator.Ratio(sample with { GridKwh = 0 }, batteryCapacityKwh: 75));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.42, false)]  // impossibly lossy — capacity is probably wrong
    [InlineData(1.15, false)]  // more energy in the battery than the meter saw
    [InlineData(0.88, true)]
    public void IsPlausible_RejectsImpossibleEstimates(double? efficiency, bool expected) =>
        Assert.Equal(expected, EfficiencyEstimator.IsPlausible(efficiency));
}
