namespace HASmartCharge.Application.Commands;

public sealed record CompleteChargingSessionCommand(
    int TransactionId,
    int MeterStopWh,
    string? StopReason,
    DateTimeOffset CompletedAt);
