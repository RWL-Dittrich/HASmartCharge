namespace HASmartCharge.Application.Commands;

public sealed record RegisterChargerCommand(
    string ChargePointId,
    string Vendor,
    string Model,
    string? SerialNumber,
    string? FirmwareVersion);
