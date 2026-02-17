using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class SetChargingProfileRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("csChargingProfiles")]
    public ChargingProfile CsChargingProfiles { get; set; } = new();
}

public class SetChargingProfileResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected, NotSupported
}
