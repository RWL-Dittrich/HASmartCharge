namespace HASmartCharge.Domain.Entities;

public sealed class Connector
{
    public string ChargePointId { get; private set; }
    public int ConnectorId { get; private set; }
    public string Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public int? ActiveTransactionId { get; private set; }
    public string? AuthorizationTag { get; private set; }
    public DateTimeOffset? SessionStartedAt { get; private set; }
    public DateTimeOffset LastStatusUpdatedAt { get; private set; }

    internal Connector(string chargePointId, int connectorId, string status, string? errorCode)
    {
        ChargePointId = chargePointId;
        ConnectorId = connectorId;
        Status = status;
        ErrorCode = errorCode;
        LastStatusUpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void UpdateStatus(string status, string? errorCode)
    {
        Status = status;
        ErrorCode = errorCode;
        LastStatusUpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void BeginSession(int transactionId, string authorizationTag, DateTimeOffset startedAt)
    {
        ActiveTransactionId = transactionId;
        AuthorizationTag = authorizationTag;
        SessionStartedAt = startedAt;
    }

    internal void EndSession()
    {
        ActiveTransactionId = null;
        AuthorizationTag = null;
        SessionStartedAt = null;
    }
}
