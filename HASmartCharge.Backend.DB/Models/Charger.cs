using System.ComponentModel.DataAnnotations;

namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Represents a charge point that has connected to the system
/// </summary>
public class Charger
{
    /// <summary>
    /// Unique charge point identifier (from the OCPP WebSocket URL)
    /// </summary>
    [Key]
    public required string ChargePointId { get; set; }

    /// <summary>
    /// Charger vendor (from BootNotification)
    /// </summary>
    public string? Vendor { get; set; }

    /// <summary>
    /// Charger model (from BootNotification)
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Serial number (from BootNotification)
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Firmware version (from BootNotification)
    /// </summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// When this charger was first seen by the system
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// When this charger last connected via WebSocket
    /// </summary>
    public DateTime LastConnectedAt { get; set; }

    /// <summary>
    /// When the last BootNotification was received (null if never)
    /// </summary>
    public DateTime? LastBootNotificationAt { get; set; }

    /// <summary>
    /// Navigation property for connectors
    /// </summary>
    public List<Connector> Connectors { get; set; } = [];

    /// <summary>
    /// Navigation property for transactions
    /// </summary>
    public List<ChargingTransaction> Transactions { get; set; } = [];
}

