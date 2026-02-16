using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class HeartbeatRequest
{
    // Empty payload
}

public class HeartbeatResponse
{
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
}
