namespace HASmartCharge.Domain.Entities;

public sealed class ChargingSession
{
    public int TransactionId { get; private set; }
    public string ChargePointId { get; private set; }
    public int ConnectorId { get; private set; }
    public string IdTag { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public int MeterStartWh { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int? MeterStopWh { get; private set; }
    public string? StopReason { get; private set; }
    public bool IsActive => CompletedAt is null;

    private ChargingSession() { ChargePointId = ""; IdTag = ""; }

    public static ChargingSession Begin(int transactionId, string chargePointId, int connectorId, string idTag, int meterStartWh, DateTimeOffset startedAt)
    {
        return new ChargingSession
        {
            TransactionId = transactionId,
            ChargePointId = chargePointId,
            ConnectorId = connectorId,
            IdTag = idTag,
            MeterStartWh = meterStartWh,
            StartedAt = startedAt
        };
    }

    public void Complete(int meterStopWh, string? stopReason, DateTimeOffset completedAt)
    {
        if (!IsActive) return;
        MeterStopWh = meterStopWh;
        StopReason = stopReason;
        CompletedAt = completedAt;
    }

    /// <summary>Reconstitutes a session from persisted state. Does not raise domain events.</summary>
    public static ChargingSession Reconstitute(
        int transactionId, string chargePointId, int connectorId, string idTag,
        int meterStartWh, DateTimeOffset startedAt,
        int? meterStopWh, string? stopReason, DateTimeOffset? completedAt)
    {
        ChargingSession session = new ChargingSession
        {
            TransactionId = transactionId,
            ChargePointId = chargePointId,
            ConnectorId = connectorId,
            IdTag = idTag,
            MeterStartWh = meterStartWh,
            StartedAt = startedAt,
            MeterStopWh = meterStopWh,
            StopReason = stopReason,
            CompletedAt = completedAt
        };
        return session;
    }
}
