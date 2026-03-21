namespace HASmartCharge.Application.Commands;

public sealed record BeginChargingSessionCommand(
    string ChargePointId,
    int ConnectorId,
    string IdTag,
    int MeterStartWh,
    DateTimeOffset StartedAt);
