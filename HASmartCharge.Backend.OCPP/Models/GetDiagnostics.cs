using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class GetDiagnosticsRequest
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("retries")]
    public int? Retries { get; set; }
    
    [JsonPropertyName("retryInterval")]
    public int? RetryInterval { get; set; }
    
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }
    
    [JsonPropertyName("stopTime")]
    public DateTime? StopTime { get; set; }
}

public class GetDiagnosticsResponse
{
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
}
