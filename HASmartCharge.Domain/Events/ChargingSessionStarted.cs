namespace HASmartCharge.Domain.Events;

public sealed record ChargingSessionStarted(
    int TransactionId,
    string ChargePointId,
    int ConnectorId,
    string IdTag,
    int MeterStartWh,
    DateTimeOffset OccurredAt) : IDomainEvent;
