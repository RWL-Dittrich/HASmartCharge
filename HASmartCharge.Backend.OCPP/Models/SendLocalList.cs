using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class AuthorizationData
{
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
    
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo? IdTagInfo { get; set; }
}

public class SendLocalListRequest
{
    [JsonPropertyName("listVersion")]
    public int ListVersion { get; set; }
    
    [JsonPropertyName("localAuthorizationList")]
    public List<AuthorizationData>? LocalAuthorizationList { get; set; }
    
    [JsonPropertyName("updateType")]
    public string UpdateType { get; set; } = string.Empty; // Differential, Full
}

public class SendLocalListResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Failed, NotSupported, VersionMismatch
}
