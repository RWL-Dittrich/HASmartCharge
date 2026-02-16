using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ReserveNowRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("expiryDate")]
    public DateTime ExpiryDate { get; set; }
    
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
    
    [JsonPropertyName("parentIdTag")]
    public string? ParentIdTag { get; set; }
    
    [JsonPropertyName("reservationId")]
    public int ReservationId { get; set; }
}

public class ReserveNowResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Faulted, Occupied, Rejected, Unavailable
}
