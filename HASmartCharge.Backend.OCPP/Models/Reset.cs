using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ResetRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Hard, Soft
}

public class ResetResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}
