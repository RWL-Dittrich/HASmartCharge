using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class StopTransactionRequest
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
    
    [JsonPropertyName("idTag")]
    public string? IdTag { get; set; }
    
    [JsonPropertyName("meterStop")]
    public int MeterStop { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
    
    [JsonPropertyName("transactionData")]
    public List<MeterValue>? TransactionData { get; set; }
}

public class StopTransactionResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo? IdTagInfo { get; set; }
}
