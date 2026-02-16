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
