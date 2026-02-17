using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class UpdateFirmwareRequest
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("retries")]
    public int? Retries { get; set; }
    
    [JsonPropertyName("retrieveDate")]
    public DateTime RetrieveDate { get; set; }
    
    [JsonPropertyName("retryInterval")]
    public int? RetryInterval { get; set; }
}

public class UpdateFirmwareResponse
{
    // Empty payload
}
