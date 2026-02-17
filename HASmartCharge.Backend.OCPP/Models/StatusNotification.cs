using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class StatusNotificationRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = "NoError";
    
    [JsonPropertyName("info")]
    public string? Info { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
    
    [JsonPropertyName("vendorId")]
    public string? VendorId { get; set; }
    
    [JsonPropertyName("vendorErrorCode")]
    public string? VendorErrorCode { get; set; }
}

public class StatusNotificationResponse
{
    // Empty payload
}
