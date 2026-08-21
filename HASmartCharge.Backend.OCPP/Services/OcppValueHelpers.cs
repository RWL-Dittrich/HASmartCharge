using System.Text.Json;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Shared helper for reading OCPP command CALLRESULT payloads. The telemetry-value conversion
/// this class used to hold (Power.Active.Import W→kW, etc.) moved into
/// HASmartCharge.Backend.Services.Telemetry.OcppTelemetryAdapter — the only place that still
/// touches OCPP measurand shapes.
/// </summary>
public static class OcppValueHelpers
{
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
