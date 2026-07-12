namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// One OCPP transaction: telemetry + attributed cost. PK = the OCPP transaction id.
/// </summary>
public class ChargeSession
{
    public int TransactionId { get; set; }

    public string ChargePointId { get; set; } = string.Empty;

    public int ConnectorId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int MeterStartWh { get; set; }

    public int? MeterStopWh { get; set; }

    public double TotalKwh { get; set; }

    public decimal TotalCost { get; set; }

    /// <summary>
    /// Timestamp of the last meter sample folded into the hour buckets. Persisted so cost
    /// attribution can resume after a restart without the in-memory sample history — the next
    /// sample's delta is measured from here. Null until the first sample arrives.
    /// </summary>
    public DateTime? LastSampleAtUtc { get; set; }

    /// <summary>Cumulative register (kWh) of the last folded sample; pairs with <see cref="LastSampleAtUtc"/>.</summary>
    public double? LastSampleKwh { get; set; }

    /// <summary>Plan this session charged under, if any.</summary>
    public int? PlanId { get; set; }

    public ChargePlan? Plan { get; set; }

    public List<HourlyEnergyUsage> HourlyUsage { get; set; } = [];
}
