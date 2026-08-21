using System.Collections.Concurrent;

namespace HASmartCharge.Backend.Services.Telemetry;

/// <summary>
/// Represents the current status and measurands of a charger
/// </summary>
public class ChargerStatus
{
    public string ChargerId { get; set; } = string.Empty;

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// When the charger last proved liveness (OCPP Heartbeat, or a successful Zaptec poll).
    /// Distinct from <see cref="LastUpdated"/> (any telemetry): an idle charger sends no meter
    /// samples, so this is what the dashboard/MQTT "last heartbeat" reflects.
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    public bool IsConnected { get; set; }

    public DateTime? ConnectedAt { get; set; }

    public DateTime? DisconnectedAt { get; set; }

    /// <summary>Charger hardware/firmware identity (e.g. from OCPP BootNotification).</summary>
    public ChargerDeviceInfo? Info { get; set; }

    /// <summary>
    /// Status per connector (key is connectorId)
    /// </summary>
    public ConcurrentDictionary<int, ConnectorStatus> Connectors { get; set; } = new();

    /// <summary>
    /// Latest measurands per connector (key is connectorId)
    /// </summary>
    public ConcurrentDictionary<int, ConnectorMeasurands> Measurands { get; set; } = new();
}

/// <summary>
/// Status of a single connector
/// </summary>
public class ConnectorStatus
{
    public int ConnectorId { get; set; }

    public string Status { get; set; } = "Unknown"; // Available, Preparing, Charging, SuspendedEVSE, SuspendedEV, Finishing, Reserved, Unavailable, Faulted

    public string ErrorCode { get; set; } = "NoError";

    public DateTime LastStatusUpdate { get; set; }

    public int? ActiveTransactionId { get; set; }

    public DateTime? TransactionStartTime { get; set; }

    /// <summary>
    /// Energy register (kWh) at transaction start — session energy = current register − this.
    /// </summary>
    public double? MeterStartKwh { get; set; }

    public string? IdTag { get; set; }
}

/// <summary>
/// Latest normalized measurands for a connector — the read-model shape of
/// <see cref="ChargerMeterSample"/> (already unit-converted; no OCPP-specific fields).
/// </summary>
public class ConnectorMeasurands
{
    public int ConnectorId { get; set; }

    public DateTime LastUpdated { get; set; }

    public double? EnergyRegisterKwh { get; set; }
    public double? PowerKw { get; set; }
    public double? VoltageL1 { get; set; }
    public double? VoltageL2 { get; set; }
    public double? VoltageL3 { get; set; }
    public double? CurrentL1 { get; set; }
    public double? CurrentL2 { get; set; }
    public double? CurrentL3 { get; set; }
    public double? SocPercent { get; set; }
}
