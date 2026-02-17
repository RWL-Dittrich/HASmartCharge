using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class RemoteStopTransactionRequest
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
}

public class RemoteStopTransactionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}
