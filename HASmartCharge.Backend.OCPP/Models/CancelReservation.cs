using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

public class CancelReservationRequest
{
    [JsonPropertyName("reservationId")]
    public int ReservationId { get; set; }
}

public class CancelReservationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Accepted, Rejected
}
