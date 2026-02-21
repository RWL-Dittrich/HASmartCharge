namespace HASmartCharge.Backend.Models.Charger;

/// <summary>
/// Request model for setting the availability of a connector
/// </summary>
public class SetAvailabilityRequest
{
    /// <summary>
    /// Availability type: "Operative" or "Inoperative"
    /// </summary>
    public string Type { get; set; } = "Operative";
}

