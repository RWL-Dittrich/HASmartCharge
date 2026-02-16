using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class GetLocalListVersionRequest
{
    // Empty payload
}

public class GetLocalListVersionResponse
{
    [JsonPropertyName("listVersion")]
    public int ListVersion { get; set; }
}
