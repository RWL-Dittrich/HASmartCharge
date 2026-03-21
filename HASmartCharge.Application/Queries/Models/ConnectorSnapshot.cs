namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for a single connector.
/// </summary>
public sealed record ConnectorSnapshot
{
    public int ConnectorId { get; init; }
    public string Status { get; init; } = "Unknown";
    public string ErrorCode { get; init; } = "NoError";
    public string? Info { get; init; }
    public string? VendorId { get; init; }
    public string? VendorErrorCode { get; init; }
    public DateTime LastStatusUpdate { get; init; }
    public int? ActiveSessionId { get; init; }
    public DateTime? SessionStartedAt { get; init; }
    public string? AuthorizationTag { get; init; }
    public ConnectorMeasurementsSnapshot? Measurements { get; init; }
}
