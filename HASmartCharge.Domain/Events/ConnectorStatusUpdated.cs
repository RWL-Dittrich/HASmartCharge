namespace HASmartCharge.Domain.Events;

public sealed record ConnectorStatusUpdated(
    string ChargePointId,
    int ConnectorId,
    string Status,
    string? ErrorCode,
    DateTimeOffset OccurredAt) : IDomainEvent;
