namespace HASmartCharge.Domain.Events;

public sealed record ChargerDisconnected(
    string ChargePointId,
    DateTimeOffset OccurredAt) : IDomainEvent;
