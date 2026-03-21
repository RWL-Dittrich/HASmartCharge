namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for a charger and its current connector state.
/// </summary>
public sealed record ChargerSnapshot
{
    public required string ChargerId { get; init; }
    public DateTime LastUpdated { get; init; }
    public bool IsConnected { get; init; }
    public DateTime? ConnectedAt { get; init; }
    public DateTime? DisconnectedAt { get; init; }
    public ChargerInfoSnapshot? Info { get; init; }
    public IReadOnlyList<ConnectorSnapshot> Connectors { get; init; } = Array.Empty<ConnectorSnapshot>();
}
