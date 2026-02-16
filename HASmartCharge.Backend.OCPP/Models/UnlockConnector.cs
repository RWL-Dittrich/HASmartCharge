using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class UnlockConnectorRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
}

public class UnlockConnectorResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Unlocked, UnlockFailed, NotSupported
}
