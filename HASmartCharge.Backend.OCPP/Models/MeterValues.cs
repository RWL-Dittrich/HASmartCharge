using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class MeterValuesRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("transactionId")]
    public int? TransactionId { get; set; }
    
    [JsonPropertyName("meterValue")]
    public List<MeterValue> MeterValue { get; set; } = new();
}

public class MeterValuesResponse
{
    // Empty payload
}
