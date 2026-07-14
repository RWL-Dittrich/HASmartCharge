using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Shared helpers for reading OCPP telemetry values and command CALLRESULT payloads.
/// Extracted from <c>ChargerController</c> so the read API and the MQTT publisher use the
/// exact same conversion/parsing logic.
/// </summary>
public static class OcppValueHelpers
{
    /// <summary>OCPP's default unit for Power.Active.Import is watts; convert to kW unless the charger already reports kW.</summary>
    public static double? ToKw(MeasurandValue? value)
    {
        if (value?.AsDecimal() is not { } raw)
        {
            return null;
        }

        var kw = (double)raw;
        if (!string.Equals(value.Unit, "kW", StringComparison.OrdinalIgnoreCase))
        {
            kw /= 1000;
        }

        return kw;
    }

    /// <summary>Reads the "status" string from an OCPP CALLRESULT payload, if present.</summary>
    public static string? ReadStatus(JsonElement? payload)
    {
        if (payload is { } el
            && el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty("status", out var statusEl)
            && statusEl.ValueKind == JsonValueKind.String)
        {
            return statusEl.GetString();
        }

        return null;
    }
}
