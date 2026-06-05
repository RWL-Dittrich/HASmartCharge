using HASmartCharge.Domain.Events;

namespace HASmartCharge.Domain.Entities;

public sealed class Charger
{
    private readonly List<IDomainEvent> _events = [];
    private readonly List<Connector> _connectors = [];

    public string ChargePointId { get; private set; }
    public string Vendor { get; private set; }
    public string Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? FirmwareVersion { get; private set; }
    public bool IsConnected { get; private set; }
    public DateTimeOffset? LastConnectedAt { get; private set; }
    public DateTimeOffset? LastDisconnectedAt { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public IReadOnlyList<Connector> Connectors => _connectors.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.AsReadOnly();

    private Charger() { ChargePointId = ""; Vendor = ""; Model = ""; }

    public static Charger Register(string chargePointId, string vendor, string model, string? serialNumber, string? firmwareVersion)
    {
        var charger = new Charger
        {
            ChargePointId = chargePointId,
            Vendor = vendor,
            Model = model,
            SerialNumber = serialNumber,
            FirmwareVersion = firmwareVersion,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        charger._events.Add(new ChargerRegistered(chargePointId, vendor, model, serialNumber, firmwareVersion, charger.RegisteredAt));
        return charger;
    }

    public void Connect()
    {
        IsConnected = true;
        LastConnectedAt = DateTimeOffset.UtcNow;
        _events.Add(new ChargerConnected(ChargePointId, LastConnectedAt.Value));
    }

    public void Disconnect()
    {
        IsConnected = false;
        LastDisconnectedAt = DateTimeOffset.UtcNow;
        _events.Add(new ChargerDisconnected(ChargePointId, LastDisconnectedAt.Value));
    }

    public void UpdateHardwareInfo(string vendor, string model, string? serialNumber, string? firmwareVersion)
    {
        Vendor = vendor;
        Model = model;
        SerialNumber = serialNumber;
        FirmwareVersion = firmwareVersion;
    }

    public Connector AddOrUpdateConnector(int connectorId, string status, string? errorCode)
    {
        var existing = _connectors.FirstOrDefault(c => c.ConnectorId == connectorId);
        if (existing is null)
        {
            var connector = new Connector(ChargePointId, connectorId, status, errorCode);
            _connectors.Add(connector);
            return connector;
        }
        existing.UpdateStatus(status, errorCode);
        return existing;
    }

    public void ClearEvents() => _events.Clear();

    /// <summary>Reconstitutes a charger from persisted state. Does not raise domain events.</summary>
    public static Charger Reconstitute(
        string chargePointId,
        string vendor,
        string model,
        string? serialNumber,
        string? firmwareVersion,
        bool isConnected,
        DateTimeOffset? lastConnectedAt,
        DateTimeOffset? lastDisconnectedAt,
        DateTimeOffset registeredAt,
        IEnumerable<(int ConnectorId, string Status, string? ErrorCode)>? connectors = null)
    {
        var charger = new Charger
        {
            ChargePointId = chargePointId,
            Vendor = vendor,
            Model = model,
            SerialNumber = serialNumber,
            FirmwareVersion = firmwareVersion,
            IsConnected = isConnected,
            LastConnectedAt = lastConnectedAt,
            LastDisconnectedAt = lastDisconnectedAt,
            RegisteredAt = registeredAt
        };
        if (connectors is not null)
            foreach ((var id, var status, var err) in connectors)
                charger.AddOrUpdateConnector(id, status, err);
        return charger;
    }
}
