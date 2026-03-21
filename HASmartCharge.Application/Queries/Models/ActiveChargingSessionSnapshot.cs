namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for the dashboard's active charging-session view.
/// </summary>
public sealed record ActiveChargingSessionSnapshot
{
    public required string ChargerId { get; init; }
    public int ConnectorId { get; init; }
    public required int SessionId { get; init; }
    public string? AuthorizationTag { get; init; }
    public DateTime? StartedAt { get; init; }
    public string ConnectorStatus { get; init; } = "Unknown";
    public ConnectorMeasurementsSnapshot? Measurements { get; init; }
}
