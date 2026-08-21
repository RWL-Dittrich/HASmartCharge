using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class RemoteStartTransactionRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }

    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
}

public class RemoteStartTransactionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}

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
