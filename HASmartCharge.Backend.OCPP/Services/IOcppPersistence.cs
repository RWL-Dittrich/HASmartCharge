namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Abstraction for persisting OCPP-related data.
/// Implemented in the DB project, consumed in the OCPP project — keeps OCPP layer DB-free.
/// Each method is a self-contained unit of work (creates its own scope/context).
/// </summary>
public interface IOcppPersistence
{
    /// <summary>
    /// Upsert a charger record. Called on WebSocket connect (bootInfo null)
    /// and again on BootNotification (bootInfo populated).
    /// </summary>
    Task UpsertChargerAsync(string chargePointId, OcppBootInfo? bootInfo, CancellationToken ct = default);

    /// <summary>
    /// Upsert a connector record. Called on StatusNotification.
    /// </summary>
    Task UpsertConnectorAsync(string chargePointId, int connectorId, string? status, string? errorCode, CancellationToken ct = default);

    /// <summary>
    /// Create a new transaction row and return the DB-assigned ID
    /// (used as the OCPP transactionId sent back to the charger).
    /// </summary>
    Task<int> BeginTransactionAsync(string chargePointId, int connectorId, string idTag, DateTime startTime, int meterStartWh, CancellationToken ct = default);

    /// <summary>
    /// Mark a transaction as completed.
    /// </summary>
    Task CompleteTransactionAsync(int transactionId, DateTime stopTime, int meterStopWh, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Get all known chargers (with their connectors) for startup seeding.
    /// </summary>
    Task<List<PersistedCharger>> GetAllChargersAsync(CancellationToken ct = default);
}

/// <summary>
/// Boot notification data passed through the abstraction layer
/// </summary>
public class OcppBootInfo
{
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
}

/// <summary>
/// Lightweight DTO returned by GetAllChargersAsync for startup seeding
/// </summary>
public class PersistedCharger
{
    public string ChargePointId { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastConnectedAt { get; set; }
    public List<PersistedConnector> Connectors { get; set; } = [];
}

/// <summary>
/// Lightweight DTO for a persisted connector
/// </summary>
public class PersistedConnector
{
    public int ConnectorId { get; set; }
    public string? LastStatus { get; set; }
    public string? LastErrorCode { get; set; }
}


