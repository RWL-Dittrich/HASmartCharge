namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for charger hardware and firmware metadata.
/// </summary>
public sealed record ChargerInfoSnapshot
{
    public string? Vendor { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? FirmwareVersion { get; init; }
    public string? Iccid { get; init; }
    public string? Imsi { get; init; }
    public string? MeterType { get; init; }
    public string? MeterSerialNumber { get; init; }
}
