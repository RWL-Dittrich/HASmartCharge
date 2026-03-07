using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Represents a connector (port) on a charge point
/// </summary>
[Index(nameof(ChargePointId), nameof(ConnectorId), IsUnique = true)]
public class Connector
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The charge point this connector belongs to
    /// </summary>
    public required string ChargePointId { get; set; }

    /// <summary>
    /// OCPP connector ID (1-based, 0 = charge point itself)
    /// </summary>
    public int ConnectorId { get; set; }

    /// <summary>
    /// Last known status (Available, Preparing, Charging, etc.)
    /// </summary>
    public string? LastStatus { get; set; }

    /// <summary>
    /// Last known error code
    /// </summary>
    public string? LastErrorCode { get; set; }

    /// <summary>
    /// When this connector was first seen
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// When the last status update was received
    /// </summary>
    public DateTime? LastStatusUpdateAt { get; set; }

    /// <summary>
    /// Navigation property to the charger
    /// </summary>
    [ForeignKey(nameof(ChargePointId))]
    public Charger Charger { get; set; } = null!;
}

