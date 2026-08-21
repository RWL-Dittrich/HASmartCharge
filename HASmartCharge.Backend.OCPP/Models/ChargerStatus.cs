namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// Charger hardware and firmware information, reported via BootNotification.
/// The live status/measurand read model (formerly also in this file) moved to
/// HASmartCharge.Backend.Services.Telemetry — Backend.OCPP no longer holds any in-memory charger
/// status; it only builds this DTO and hands it to <see cref="IChargerTelemetrySink.OnBoot"/>.
/// </summary>
public class ChargerInfo
{
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? Iccid { get; set; }
    public string? Imsi { get; set; }
    public string? MeterType { get; set; }
    public string? MeterSerialNumber { get; set; }
}
