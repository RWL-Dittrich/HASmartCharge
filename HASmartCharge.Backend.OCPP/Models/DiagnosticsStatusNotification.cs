using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class DiagnosticsStatusNotificationRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Idle, Uploaded, UploadFailed, Uploading
}

public class DiagnosticsStatusNotificationResponse
{
    // Empty payload
}
