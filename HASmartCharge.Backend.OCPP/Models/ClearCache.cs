using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ClearCacheRequest
{
    // Empty payload
}

public class ClearCacheResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}
