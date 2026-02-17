using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class RemoteStartTransactionRequest
{
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; set; }
    
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
    
    [JsonPropertyName("chargingProfile")]
    public ChargingProfile? ChargingProfile { get; set; }
}

public class RemoteStartTransactionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}
