namespace HASmartCharge.Core.Calibration;

/// <summary>One completed charge session used as an efficiency data point.</summary>
/// <param name="StartSocPercent">Car SoC when the transaction started.</param>
/// <param name="EndSocPercent">Car SoC when the transaction finished.</param>
/// <param name="GridKwh">Metered kWh the charger delivered during the transaction.</param>
public readonly record struct EfficiencySample(double StartSocPercent, double EndSocPercent, double GridKwh);

/// <param name="Efficiency">Battery kWh ÷ grid kWh, or null when no session qualified.</param>
/// <param name="SampleCount">How many sessions were actually used.</param>
public readonly record struct EfficiencyEstimate(double? Efficiency, int SampleCount, double BatteryKwh, double GridKwh);

/// <summary>
/// Derives the real grid → battery charge efficiency from measured sessions:
/// energy that landed in the battery (SoC delta × capacity) over energy the meter recorded.
/// <para>
/// This attributes ALL of the error to efficiency — a wrong <c>BatteryCapacityKwh</c> shows up
/// here as a wrong efficiency, since one measurement can't separate the two. Small sessions are
/// dropped because SoC-reading error — quantization plus however stale the car's last report was —
/// is a large relative error on them.
/// </para>
/// </summary>
public static class EfficiencyEstimator
{
    /// <summary>
    /// Sessions charging less than this much SoC are too noisy to use. Sized against the two error
    /// sources in a SoC reading — 1% quantization, and a reading the car reported up to ~10 minutes
    /// before it was read (~2.4 %-SoC at 11 kW) — so the worst case stays around a tenth of the
    /// delta being measured. Small top-ups are exactly the sessions those errors ruin.
    /// </summary>
    public const double MinSocDeltaPercent = 20;

    /// <summary>Sessions delivering less than this are too noisy to use.</summary>
    public const double MinGridKwh = 1;

    /// <summary>Range an estimate must fall in to be worth applying (charging is lossy, never a gain).</summary>
    public const double MinPlausible = 0.5;
    public const double MaxPlausible = 1.0;

    public static EfficiencyEstimate Estimate(IEnumerable<EfficiencySample> samples, double batteryCapacityKwh)
    {
        if (batteryCapacityKwh <= 0)
        {
            return new EfficiencyEstimate(null, 0, 0, 0);
        }

        var batteryKwh = 0.0;
        var gridKwh = 0.0;
        var count = 0;

        foreach (var sample in samples)
        {
            if (!IsUsable(sample))
            {
                continue;
            }

            var socDelta = sample.EndSocPercent - sample.StartSocPercent;
            batteryKwh += socDelta / 100.0 * batteryCapacityKwh;
            gridKwh += sample.GridKwh;
            count++;
        }

        // Pooled (energy-weighted) rather than a mean of per-session ratios, so long sessions —
        // which carry less SoC-rounding noise — count for more.
        return count == 0 || gridKwh <= 0
            ? new EfficiencyEstimate(null, 0, 0, 0)
            : new EfficiencyEstimate(batteryKwh / gridKwh, count, batteryKwh, gridKwh);
    }

    /// <summary>
    /// True when a session is clean enough to feed <see cref="Estimate"/>. A session that fails
    /// this still has a <see cref="Ratio"/> worth showing — it just carries too much SoC-reading
    /// error to average into the number the user might apply.
    /// </summary>
    public static bool IsUsable(EfficiencySample sample) =>
        sample.EndSocPercent - sample.StartSocPercent >= MinSocDeltaPercent && sample.GridKwh >= MinGridKwh;

    /// <summary>
    /// One session's grid → battery ratio, with no noise filtering, for displaying that session on
    /// its own. Null when it can't be computed at all (no capacity, no metered energy).
    /// </summary>
    public static double? Ratio(EfficiencySample sample, double batteryCapacityKwh)
    {
        if (batteryCapacityKwh <= 0 || sample.GridKwh <= 0)
        {
            return null;
        }

        return (sample.EndSocPercent - sample.StartSocPercent) / 100.0 * batteryCapacityKwh / sample.GridKwh;
    }

    /// <summary>True when an estimate is physically sensible enough to write into settings.</summary>
    public static bool IsPlausible(double? efficiency) =>
        efficiency is { } value && value >= MinPlausible && value <= MaxPlausible;
}
