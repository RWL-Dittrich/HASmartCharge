namespace HASmartCharge.Domain.Events;

public sealed record ChargerConnected(
    string ChargePointId,
    DateTimeOffset OccurredAt) : IDomainEvent;
