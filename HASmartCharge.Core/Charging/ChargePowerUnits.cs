using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HASmartCharge.Core.Charging;

/// <summary>
/// Formats a charge-power setpoint as the string value of a vendor configuration key
/// (e.g. USER_PMAX), in whichever unit that key expects.
/// </summary>
public static class ChargePowerUnits
{
    public static readonly string[] Supported = ["A", "mA", "W", "kW"];

    /// <summary>
    /// Converts the setpoint to <paramref name="unit"/> and formats it as an OCPP configuration value.
    /// Always rounds down so the charger is never asked to exceed the setpoint. Current-based units
    /// take the pre-computed <paramref name="amps"/> (which already carries the phase/voltage math).
    /// A and kW keep one decimal; mA and W are whole numbers.
    /// </summary>
    public static bool TryFormat(string? unit, double kw, double amps, [NotNullWhen(true)] out string? value)
    {
        double raw;
        int decimals;

        switch (unit?.Trim().ToLowerInvariant())
        {
            case "a": raw = amps; decimals = 1; break;
            case "ma": raw = amps * 1000.0; decimals = 0; break;
            case "w": raw = kw * 1000.0; decimals = 0; break;
            case "kw": raw = kw; decimals = 1; break;
            default: value = null; return false;
        }

        var rounded = Math.Round(raw, decimals, MidpointRounding.ToZero);

        // Trim a trailing ".0": strict chargers reject a decimal on an integer-typed key.
        value = rounded == Math.Floor(rounded)
            ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
            : rounded.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        return true;
    }
}
