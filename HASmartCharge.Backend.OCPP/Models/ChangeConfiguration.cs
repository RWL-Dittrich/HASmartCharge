using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ChangeConfigurationRequest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class ChangeConfigurationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected, RebootRequired, NotSupported
}
