namespace HASmartCharge.Domain.Events;

public sealed record ChargerRegistered(
    string ChargePointId,
    string Vendor,
    string Model,
    string? SerialNumber,
    string? FirmwareVersion,
    DateTimeOffset OccurredAt) : IDomainEvent;
