using System.Text.Json.Serialization;

namespace HASmartChargeBackend.Models.HomeAssistant;

public class HaEntity
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }
    [JsonPropertyName("state")]
    public required string State { get; set; }
    [JsonPropertyName("attributes")]
    public required Dictionary<string, object> Attributes { get; set; }
    [JsonPropertyName("last_changed")]
    public required DateTime LastChanged { get; set; }
    
    public HaEntityType EntityType => GetEntityType();
    
    private HaEntityType GetEntityType()
    {
        if (EntityId.StartsWith("sensor."))
            return HaEntityType.Sensor;
        if (EntityId.StartsWith("switch."))
            return HaEntityType.Switch;
        if (EntityId.StartsWith("binary_sensor."))
            return HaEntityType.BinarySensor;
        return HaEntityType.Unknown;
    }
}

public enum HaEntityType
{
    Sensor,
    Switch,
    BinarySensor,
    Unknown
}