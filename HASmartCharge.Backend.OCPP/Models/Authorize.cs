using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class AuthorizeRequest
{
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
}

public class AuthorizeResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; set; } = new();
}
