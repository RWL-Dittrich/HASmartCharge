namespace HASmartCharge.Domain.Events;

public sealed record ChargingSessionCompleted(
    int TransactionId,
    string ChargePointId,
    int ConnectorId,
    int MeterStopWh,
    string? StopReason,
    DateTimeOffset OccurredAt) : IDomainEvent;
