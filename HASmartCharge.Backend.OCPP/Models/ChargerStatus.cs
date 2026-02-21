using System.Collections.Concurrent;

namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// Represents the current status and measurands of a charger
/// </summary>
public class ChargerStatus
{
    public string ChargePointId { get; set; } = string.Empty;
    
    public DateTime LastUpdated { get; set; }
    
    public bool IsConnected { get; set; }
    
    public DateTime? ConnectedAt { get; set; }
    
    public DateTime? DisconnectedAt { get; set; }
    
    /// <summary>
    /// Charger information from BootNotification
    /// </summary>
    public ChargerInfo? Info { get; set; }
    
    /// <summary>
    /// Status per connector (key is connectorId)
    /// </summary>
    public ConcurrentDictionary<int, ConnectorStatus> Connectors { get; set; } = new();
    
    /// <summary>
    /// Latest measurands per connector (key is connectorId)
    /// </summary>
    public ConcurrentDictionary<int, ConnectorMeasurands> Measurands { get; set; } = new();
}

/// <summary>
/// Charger hardware and firmware information
/// </summary>
public class ChargerInfo
{
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? Iccid { get; set; }
    public string? Imsi { get; set; }
    public string? MeterType { get; set; }
    public string? MeterSerialNumber { get; set; }
}

/// <summary>
/// Status of a single connector
/// </summary>
public class ConnectorStatus
{
    public int ConnectorId { get; set; }
    
    public string Status { get; set; } = "Unknown"; // Available, Preparing, Charging, SuspendedEVSE, SuspendedEV, Finishing, Reserved, Unavailable, Faulted
    
    public string ErrorCode { get; set; } = "NoError";
    
    public string? Info { get; set; }
    
    public string? VendorId { get; set; }
    
    public string? VendorErrorCode { get; set; }
    
    public DateTime LastStatusUpdate { get; set; }
    
    public int? ActiveTransactionId { get; set; }
    
    public DateTime? TransactionStartTime { get; set; }
    
    public string? IdTag { get; set; }
}

/// <summary>
/// Measurand values for a connector
/// </summary>
public class ConnectorMeasurands
{
    public int ConnectorId { get; set; }
    
    public DateTime LastUpdated { get; set; }
    
    // Energy measurements
    public MeasurandValue? EnergyActiveImportRegister { get; set; }
    public MeasurandValue? EnergyReactiveImportRegister { get; set; }
    public MeasurandValue? EnergyActiveExportRegister { get; set; }
    public MeasurandValue? EnergyReactiveExportRegister { get; set; }
    
    // Power measurements
    public MeasurandValue? PowerActiveImport { get; set; }
    public MeasurandValue? PowerReactiveImport { get; set; }
    public MeasurandValue? PowerOffered { get; set; }
    
    // Voltage measurements per phase
    public MeasurandValue? VoltageL1 { get; set; }
    public MeasurandValue? VoltageL2 { get; set; }
    public MeasurandValue? VoltageL3 { get; set; }
    public MeasurandValue? VoltageL1N { get; set; }
    public MeasurandValue? VoltageL2N { get; set; }
    public MeasurandValue? VoltageL3N { get; set; }
    
    // Current measurements per phase
    public MeasurandValue? CurrentImportL1 { get; set; }
    public MeasurandValue? CurrentImportL2 { get; set; }
    public MeasurandValue? CurrentImportL3 { get; set; }
    public MeasurandValue? CurrentExportL1 { get; set; }
    public MeasurandValue? CurrentExportL2 { get; set; }
    public MeasurandValue? CurrentExportL3 { get; set; }
    public MeasurandValue? CurrentOffered { get; set; }
    
    // Other measurements
    public MeasurandValue? Temperature { get; set; }
    public MeasurandValue? SoC { get; set; } // State of Charge
    public MeasurandValue? Frequency { get; set; }
    public MeasurandValue? Rpm { get; set; }
}

/// <summary>
/// A single measurand value with metadata
/// </summary>
public class MeasurandValue
{
    public string Value { get; set; } = string.Empty;
    
    public string? Unit { get; set; }
    
    public string? Context { get; set; }
    
    public string? Format { get; set; }
    
    public string? Location { get; set; }
    
    public string? Phase { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Parse the value as a decimal if possible
    /// </summary>
    public decimal? AsDecimal()
    {
        if (decimal.TryParse(Value, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }
        return null;
    }
    
    /// <summary>
    /// Parse the value as an integer if possible
    /// </summary>
    public int? AsInt()
    {
        if (int.TryParse(Value, out int result))
        {
            return result;
        }
        return null;
    }
}

