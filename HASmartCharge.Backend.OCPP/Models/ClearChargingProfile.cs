using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ClearChargingProfileRequest
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; set; }
    
    [JsonPropertyName("chargingProfilePurpose")]
    public string? ChargingProfilePurpose { get; set; }
    
    [JsonPropertyName("stackLevel")]
    public int? StackLevel { get; set; }
}

public class ClearChargingProfileResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Unknown
}
