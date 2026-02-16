using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class TriggerMessageRequest
{
    [JsonPropertyName("requestedMessage")]
    public string RequestedMessage { get; set; } = string.Empty; // BootNotification, DiagnosticsStatusNotification, FirmwareStatusNotification, Heartbeat, MeterValues, StatusNotification
    
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; set; }
}

public class TriggerMessageResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected, NotImplemented
}
