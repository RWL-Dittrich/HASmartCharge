using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class StartTransactionRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
    
    [JsonPropertyName("meterStart")]
    public int MeterStart { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("reservationId")]
    public int? ReservationId { get; set; }
}

public class StartTransactionResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; set; } = new();
    
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
}
