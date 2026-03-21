namespace HASmartCharge.Backend.Models.Charger;

/// <summary>Request model for resetting a charger.</summary>
public class ResetChargerRequest
{
    /// <summary>Reset type: "Hard" or "Soft"</summary>
    public string Type { get; set; } = "Hard";
}

/// <summary>Request model for triggering a specific OCPP message from the charger.</summary>
public class TriggerMessageBodyRequest
{
    /// <summary>
    /// OCPP message type to trigger, e.g. BootNotification, Heartbeat, StatusNotification.
    /// </summary>
    public string RequestedMessage { get; set; } = string.Empty;

    /// <summary>Optional connector ID to scope the trigger.</summary>
    public int? ConnectorId { get; set; }
}

/// <summary>Request model for requesting a diagnostics file upload.</summary>
public class GetDiagnosticsBodyRequest
{
    /// <summary>URL to which the charger should upload the diagnostics file.</summary>
    public string Location { get; set; } = string.Empty;
}

/// <summary>Request model for remotely starting a transaction.</summary>
public class RemoteStartRequest
{
    /// <summary>RFID tag / authorisation identifier.</summary>
    public string IdTag { get; set; } = string.Empty;
}
