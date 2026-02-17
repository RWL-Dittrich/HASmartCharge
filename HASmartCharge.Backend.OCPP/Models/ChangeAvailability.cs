using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ChangeAvailabilityRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Operative, Inoperative
}

public class ChangeAvailabilityResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected, Scheduled
}
