using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

[ApiController]
[Route("api/chargers")]
public class ChargerStatusController : ControllerBase
{
    private readonly ChargerStatusTracker _statusTracker;

    public ChargerStatusController(ChargerStatusTracker statusTracker)
    {
        _statusTracker = statusTracker;
    }

    /// <summary>
    /// Get all charger statuses
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetAllStatuses()
    {
        IEnumerable<ChargerStatus> statuses = _statusTracker.GetAllChargerStatuses();
        return Ok(statuses);
    }

    /// <summary>
    /// Get status of a specific charger
    /// </summary>
    [HttpGet("{chargePointId}/status")]
    public IActionResult GetChargerStatus(string chargePointId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargePointId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Charger {chargePointId} not found" });
        }

        return Ok(status);
    }

    /// <summary>
    /// Get all connected chargers
    /// </summary>
    [HttpGet("connected")]
    public IActionResult GetConnectedChargers()
    {
        IEnumerable<ChargerStatus> statuses = _statusTracker.GetConnectedChargers();
        return Ok(statuses);
    }

    /// <summary>
    /// Get connector status for a specific charger
    /// </summary>
    [HttpGet("{chargePointId}/connectors/{connectorId}/status")]
    public IActionResult GetConnectorStatus(string chargePointId, int connectorId)
    {
        ConnectorStatus? status = _statusTracker.GetConnectorStatus(chargePointId, connectorId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Connector {connectorId} not found for charger {chargePointId}" });
        }

        return Ok(status);
    }

    /// <summary>
    /// Get measurands for a specific connector
    /// </summary>
    [HttpGet("{chargePointId}/connectors/{connectorId}/measurands")]
    public IActionResult GetConnectorMeasurands(string chargePointId, int connectorId)
    {
        ConnectorMeasurands? measurands = _statusTracker.GetConnectorMeasurands(chargePointId, connectorId);
        
        if (measurands == null)
        {
            return NotFound(new { error = $"Measurands not found for connector {connectorId} on charger {chargePointId}" });
        }

        return Ok(measurands);
    }

    /// <summary>
    /// Get all active charging sessions
    /// </summary>
    [HttpGet("charging-sessions")]
    public IActionResult GetActiveChargingSessions()
    {
        IEnumerable<(string ChargePointId, int ConnectorId)> sessions = _statusTracker.GetActiveChargingSessions();
        
        var result = sessions.Select(s => new
        {
            chargePointId = s.ChargePointId,
            connectorId = s.ConnectorId,
            status = _statusTracker.GetConnectorStatus(s.ChargePointId, s.ConnectorId),
            measurands = _statusTracker.GetConnectorMeasurands(s.ChargePointId, s.ConnectorId)
        });

        return Ok(result);
    }

    /// <summary>
    /// Get summary statistics for all chargers
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        List<ChargerStatus> allStatuses = _statusTracker.GetAllChargerStatuses().ToList();
        List<ChargerStatus> connected = _statusTracker.GetConnectedChargers().ToList();
        List<(string, int)> activeCharging = _statusTracker.GetActiveChargingSessions().ToList();

        var summary = new
        {
            totalChargers = allStatuses.Count,
            connectedChargers = connected.Count,
            disconnectedChargers = allStatuses.Count - connected.Count,
            activeChargingSessions = activeCharging.Count,
            chargers = allStatuses.Select(s => new
            {
                chargePointId = s.ChargePointId,
                isConnected = s.IsConnected,
                vendor = s.Info?.Vendor,
                model = s.Info?.Model,
                connectorCount = s.Connectors.Count,
                activeConnectors = s.Connectors.Values.Count(c => c.Status == "Charging"),
                lastUpdated = s.LastUpdated
            })
        };

        return Ok(summary);
    }

    /// <summary>
    /// Get current power consumption for a charger
    /// </summary>
    [HttpGet("{chargePointId}/power")]
    public IActionResult GetCurrentPower(string chargePointId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargePointId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Charger {chargePointId} not found" });
        }

        var powerData = status.Measurands.Select(kvp => new
        {
            connectorId = kvp.Key,
            powerActiveImport = kvp.Value.PowerActiveImport?.AsDecimal(),
            powerUnit = kvp.Value.PowerActiveImport?.Unit,
            voltage = new
            {
                l1 = kvp.Value.VoltageL1?.AsDecimal(),
                l2 = kvp.Value.VoltageL2?.AsDecimal(),
                l3 = kvp.Value.VoltageL3?.AsDecimal()
            },
            current = new
            {
                l1 = kvp.Value.CurrentImportL1?.AsDecimal(),
                l2 = kvp.Value.CurrentImportL2?.AsDecimal(),
                l3 = kvp.Value.CurrentImportL3?.AsDecimal()
            },
            lastUpdated = kvp.Value.LastUpdated
        });

        return Ok(new
        {
            chargePointId,
            connectors = powerData
        });
    }
    

    /// <summary>
    /// Get just the active power value for a specific connector (simplified response)
    /// </summary>
    [HttpGet("{chargePointId}/connectors/{connectorId}/active-power")]
    public IActionResult GetActivePower(string chargePointId, int connectorId)
    {
        ConnectorMeasurands? measurands = _statusTracker.GetConnectorMeasurands(chargePointId, connectorId);
        
        if (measurands == null)
        {
            return NotFound(new { error = $"No measurands found for connector {connectorId} on charger {chargePointId}" });
        }

        decimal? powerValue = measurands.PowerActiveImport?.AsDecimal();
        
        return Ok(new
        {
            chargePointId,
            connectorId,
            activePowerW = powerValue,
            unit = measurands.PowerActiveImport?.Unit ?? "W",
            timestamp = measurands.PowerActiveImport?.Timestamp,
            lastUpdated = measurands.LastUpdated,
            isCharging = powerValue.HasValue && powerValue.Value > 0
        });
    }

    /// <summary>
    /// Get total active power across all connectors for a charger
    /// </summary>
    [HttpGet("{chargePointId}/total-power")]
    public IActionResult GetTotalPower(string chargePointId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargePointId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Charger {chargePointId} not found" });
        }

        decimal totalPower = 0;
        var connectorPowers = new List<object>();

        foreach (var kvp in status.Measurands)
        {
            decimal? power = kvp.Value.PowerActiveImport?.AsDecimal();
            if (power.HasValue)
            {
                totalPower += power.Value;
                connectorPowers.Add(new
                {
                    connectorId = kvp.Key,
                    powerW = power.Value,
                    lastUpdated = kvp.Value.LastUpdated
                });
            }
        }

        return Ok(new
        {
            chargePointId,
            totalActivePowerW = totalPower,
            connectorCount = status.Measurands.Count,
            activeConnectors = connectorPowers.Count,
            connectors = connectorPowers,
            lastUpdated = status.LastUpdated
        });
    }

    /// <summary>
    /// Get energy consumption for a charger
    /// </summary>
    [HttpGet("{chargePointId}/energy")]
    public IActionResult GetEnergyConsumption(string chargePointId)
    {
        ChargerStatus? status = _statusTracker.GetChargerStatus(chargePointId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Charger {chargePointId} not found" });
        }

        var energyData = status.Measurands.Select(kvp => new
        {
            connectorId = kvp.Key,
            energyActiveImport = kvp.Value.EnergyActiveImportRegister?.AsDecimal(),
            energyUnit = kvp.Value.EnergyActiveImportRegister?.Unit,
            lastUpdated = kvp.Value.LastUpdated
        });

        return Ok(new
        {
            chargePointId,
            connectors = energyData
        });
    }
}





