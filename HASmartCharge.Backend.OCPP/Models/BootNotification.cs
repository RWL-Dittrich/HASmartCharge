using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class BootNotificationRequest
{
    [JsonPropertyName("chargePointVendor")]
    public string ChargePointVendor { get; set; } = string.Empty;
    
    [JsonPropertyName("chargePointModel")]
    public string ChargePointModel { get; set; } = string.Empty;
    
    [JsonPropertyName("chargePointSerialNumber")]
    public string? ChargePointSerialNumber { get; set; }
    
    [JsonPropertyName("chargeBoxSerialNumber")]
    public string? ChargeBoxSerialNumber { get; set; }
    
    [JsonPropertyName("firmwareVersion")]
    public string? FirmwareVersion { get; set; }
    
    [JsonPropertyName("iccid")]
    public string? Iccid { get; set; }
    
    [JsonPropertyName("imsi")]
    public string? Imsi { get; set; }
    
    [JsonPropertyName("meterType")]
    public string? MeterType { get; set; }
    
    [JsonPropertyName("meterSerialNumber")]
    public string? MeterSerialNumber { get; set; }
}

public class BootNotificationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Pending, Rejected
    
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 60;
}
