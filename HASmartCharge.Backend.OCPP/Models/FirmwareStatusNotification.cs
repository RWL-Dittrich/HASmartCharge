using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class FirmwareStatusNotificationRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Downloaded, DownloadFailed, Downloading, Idle, InstallationFailed, Installing, Installed
}

public class FirmwareStatusNotificationResponse
{
    // Empty payload
}
