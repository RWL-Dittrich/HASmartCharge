namespace HASmartCharge.Backend.Services.Telemetry;

/// <summary>
/// Charger-neutral connector state. Member names use the exact OCPP 1.6 StatusNotification
/// casing (<c>SuspendedEV</c>, <c>SuspendedEVSE</c>, ...) because <see cref="ChargerStatusTracker"/>
/// stores <c>state.ToString()</c> as the connector status string consumed by existing readers
/// (dashboard, MQTT) — a future Zaptec poller maps its own op-modes onto these same members.
/// </summary>
public enum ConnectorState
{
    Unknown,
    Available,
    Preparing,
    Charging,
    SuspendedEV,
    SuspendedEVSE,
    Finishing,
    Reserved,
    Unavailable,
    Faulted
}

/// <summary>Charger hardware/firmware identity, reported once (e.g. from OCPP BootNotification).</summary>
public record ChargerDeviceInfo(string? Vendor, string? Model, string? SerialNumber, string? FirmwareVersion);

/// <summary>
/// One normalized meter reading. All values are already unit-converted (kWh, kW, V, A) — the
/// producer (<see cref="OcppTelemetryAdapter"/> today, a Zaptec poller later) owns any conversion.
/// </summary>
public record ChargerMeterSample(
    double? EnergyRegisterKwh,
    double? PowerKw,
    double? VoltageL1,
    double? VoltageL2,
    double? VoltageL3,
    double? CurrentL1,
    double? CurrentL2,
    double? CurrentL3,
    double? SocPercent,
    DateTime TimestampUtc);
