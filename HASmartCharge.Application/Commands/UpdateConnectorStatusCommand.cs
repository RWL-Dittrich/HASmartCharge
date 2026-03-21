namespace HASmartCharge.Application.Commands;

public sealed record UpdateConnectorStatusCommand(
    string ChargePointId,
    int ConnectorId,
    string Status,
    string? ErrorCode);
