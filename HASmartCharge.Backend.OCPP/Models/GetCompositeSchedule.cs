using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class GetCompositeScheduleRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    
    [JsonPropertyName("chargingRateUnit")]
    public string? ChargingRateUnit { get; set; }
}

public class GetCompositeScheduleResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
    
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; set; }
    
    [JsonPropertyName("scheduleStart")]
    public DateTime? ScheduleStart { get; set; }
    
    [JsonPropertyName("chargingSchedule")]
    public ChargingSchedule? ChargingSchedule { get; set; }
}
