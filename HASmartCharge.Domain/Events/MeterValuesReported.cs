namespace HASmartCharge.Domain.Events;

public sealed record MeterValuesReported(
    string ChargePointId,
    int ConnectorId,
    int? TransactionId,
    IReadOnlyList<MeterValueEntry> Values,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record MeterValueEntry(
    string Measurand,
    string Value,
    string? Unit,
    string? Phase,
    DateTimeOffset Timestamp);
