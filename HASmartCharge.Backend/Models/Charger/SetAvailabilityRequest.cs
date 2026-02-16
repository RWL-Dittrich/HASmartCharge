namespace HASmartCharge.Backend.Models.Charger;

/// <summary>
/// Request model for setting charger availability
/// </summary>
public class SetAvailabilityRequest
{
    /// <summary>
    /// Connector ID (0 for whole charge point, >0 for specific connector)
    /// </summary>
    public int ConnectorId { get; set; } = 0;
    
    /// <summary>
    /// Availability type: "Operative" or "Inoperative"
    /// </summary>
    public string Type { get; set; } = "Operative";
}


