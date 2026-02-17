using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class DataTransferRequest
{
    [JsonPropertyName("vendorId")]
    public string VendorId { get; set; } = string.Empty;
    
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
    
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public class DataTransferResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Rejected, UnknownMessageId, UnknownVendorId
    
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
