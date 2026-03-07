using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Represents a charging transaction on a connector
/// </summary>
[Index(nameof(ChargePointId), nameof(ConnectorId))]
[Index(nameof(IdTag))]
public class ChargingTransaction
{
    /// <summary>
    /// Auto-incremented primary key — also used as the OCPP transactionId
    /// returned to the charger (survives app restarts)
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The charge point this transaction belongs to
    /// </summary>
    public required string ChargePointId { get; set; }

    /// <summary>
    /// The connector on which the transaction took place
    /// </summary>
    public int ConnectorId { get; set; }

    /// <summary>
    /// The RFID tag / identifier that authorized the session
    /// </summary>
    public required string IdTag { get; set; }

    /// <summary>
    /// When the transaction started (from the charger's clock)
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Meter reading at the start of the transaction (Wh)
    /// </summary>
    public int MeterStartWh { get; set; }

    /// <summary>
    /// When the transaction stopped (null if still in progress)
    /// </summary>
    public DateTime? StopTime { get; set; }

    /// <summary>
    /// Meter reading at the end of the transaction (Wh, null if still in progress)
    /// </summary>
    public int? MeterStopWh { get; set; }

    /// <summary>
    /// Reason the transaction was stopped (null if still in progress)
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    /// Navigation property to the charger
    /// </summary>
    [ForeignKey(nameof(ChargePointId))]
    public Charger Charger { get; set; } = null!;
}

