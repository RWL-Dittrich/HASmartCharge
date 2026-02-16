using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class ConfigurationKey
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("readonly")]
    public bool Readonly { get; set; }
    
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class GetConfigurationRequest
{
    [JsonPropertyName("key")]
    public List<string>? Key { get; set; }
}

public class GetConfigurationResponse
{
    [JsonPropertyName("configurationKey")]
    public List<ConfigurationKey>? ConfigurationKey { get; set; }
    
    [JsonPropertyName("unknownKey")]
    public List<string>? UnknownKey { get; set; }
}
