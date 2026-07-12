using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// IdTag information used across multiple OCPP messages
/// </summary>
public class IdTagInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Blocked, Expired, Invalid, ConcurrentTx
    
    [JsonPropertyName("expiryDate")]
    public DateTime? ExpiryDate { get; set; }
    
    [JsonPropertyName("parentIdTag")]
    public string? ParentIdTag { get; set; }
}

/// <summary>
/// Sampled value from a meter
/// </summary>
public class SampledValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("context")]
    public string? Context { get; set; }
    
    [JsonPropertyName("format")]
    public string? Format { get; set; }
    
    [JsonPropertyName("measurand")]
    public string? Measurand { get; set; }
    
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

/// <summary>
/// Meter value with timestamp and sampled values
/// </summary>
public class MeterValue
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("sampledValue")]
    public List<SampledValue> SampledValue { get; set; } = new();
}

/// <summary>
/// Charging schedule period
/// </summary>
public class ChargingSchedulePeriod
{
    [JsonPropertyName("startPeriod")]
    public int StartPeriod { get; set; }

    [JsonPropertyName("limit")]
    public decimal Limit { get; set; }

    // Optional OCPP fields are omitted (not sent as null) so strict chargers accept the profile.
    [JsonPropertyName("numberPhases")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumberPhases { get; set; }
}

/// <summary>
/// Charging schedule
/// </summary>
public class ChargingSchedule
{
    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; set; }

    [JsonPropertyName("startSchedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? StartSchedule { get; set; }

    [JsonPropertyName("chargingRateUnit")]
    public string ChargingRateUnit { get; set; } = string.Empty; // W, A

    [JsonPropertyName("chargingSchedulePeriod")]
    public List<ChargingSchedulePeriod> ChargingSchedulePeriod { get; set; } = new();

    [JsonPropertyName("minChargingRate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MinChargingRate { get; set; }
}

/// <summary>
/// Charging profile
/// </summary>
public class ChargingProfile
{
    [JsonPropertyName("chargingProfileId")]
    public int ChargingProfileId { get; set; }

    [JsonPropertyName("transactionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransactionId { get; set; }

    [JsonPropertyName("stackLevel")]
    public int StackLevel { get; set; }

    [JsonPropertyName("chargingProfilePurpose")]
    public string ChargingProfilePurpose { get; set; } = string.Empty; // ChargePointMaxProfile, TxDefaultProfile, TxProfile

    [JsonPropertyName("chargingProfileKind")]
    public string ChargingProfileKind { get; set; } = string.Empty; // Absolute, Recurring, Relative

    [JsonPropertyName("recurrencyKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecurrencyKind { get; set; } // Daily, Weekly

    [JsonPropertyName("validFrom")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("validTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("chargingSchedule")]
    public ChargingSchedule ChargingSchedule { get; set; } = new();
}
