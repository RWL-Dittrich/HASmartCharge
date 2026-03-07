using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Aggregated dashboard data — a single endpoint the UI can poll
/// instead of fetching every charger individually.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly ChargerStatusTracker _statusTracker;

    public DashboardController(ChargerStatusTracker statusTracker)
    {
        _statusTracker = statusTracker;
    }

    /// <summary>
    /// Returns a pre-aggregated summary of the entire charging network:
    /// charger counts, connector status breakdown, active transactions with
    /// live power/energy data, and fleet-wide totals.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSummary()
    {
        List<ChargerStatus> all = _statusTracker.GetAllChargerStatuses().ToList();

        int totalChargers = all.Count;
        int onlineChargers = all.Count(s => s.IsConnected);

        // Flatten every connector across all chargers
        List<ConnectorStatus> allConnectors = all
            .SelectMany(s => s.Connectors.Values)
            .ToList();

        // Count connectors by OCPP status
        Dictionary<string, int> connectorsByStatus = new()
        {
            ["Available"] = 0,
            ["Preparing"] = 0,
            ["Charging"] = 0,
            ["SuspendedEVSE"] = 0,
            ["SuspendedEV"] = 0,
            ["Finishing"] = 0,
            ["Reserved"] = 0,
            ["Unavailable"] = 0,
            ["Faulted"] = 0,
            ["Unknown"] = 0,
        };

        foreach (ConnectorStatus c in allConnectors)
        {
            string key = connectorsByStatus.ContainsKey(c.Status) ? c.Status : "Unknown";
            connectorsByStatus[key]++;
        }

        // Active transactions with live measurands
        var activeTransactions = _statusTracker.GetAllActiveTransactions()
            .OrderByDescending(t => t.Connector.TransactionStartTime)
            .Select(t => new
            {
                chargePointId        = t.ChargePointId,
                connectorId          = t.Connector.ConnectorId,
                transactionId        = t.Connector.ActiveTransactionId!.Value,
                idTag                = t.Connector.IdTag,
                startTime            = t.Connector.TransactionStartTime,
                connectorStatus      = t.Connector.Status,
                energyActiveImportWh = t.Measurands?.EnergyActiveImportRegister,
                powerActiveImport    = t.Measurands?.PowerActiveImport,
            })
            .ToList();

        // Fleet-wide power & energy totals from all connector measurands
        decimal totalPowerDrawKw = 0;
        decimal totalEnergyDeliveredKwh = 0;

        foreach (ChargerStatus charger in all)
        {
            foreach (ConnectorMeasurands measurands in charger.Measurands.Values)
            {
                if (measurands.PowerActiveImport?.AsDecimal() is { } power)
                {
                    string unit = measurands.PowerActiveImport.Unit ?? "W";
                    totalPowerDrawKw += unit.Equals("kW", StringComparison.OrdinalIgnoreCase)
                        ? power
                        : power / 1000m;
                }

                if (measurands.EnergyActiveImportRegister?.AsDecimal() is { } energy)
                {
                    string unit = measurands.EnergyActiveImportRegister.Unit ?? "Wh";
                    totalEnergyDeliveredKwh += unit.Equals("kWh", StringComparison.OrdinalIgnoreCase)
                        ? energy
                        : energy / 1000m;
                }
            }
        }

        return Ok(new
        {
            totalChargers,
            onlineChargers,
            offlineChargers        = totalChargers - onlineChargers,
            totalConnectors        = allConnectors.Count,
            connectorsByStatus,
            activeTransactions,
            totalActiveTransactions = activeTransactions.Count,
            totalPowerDrawKw        = Math.Round(totalPowerDrawKw, 2),
            totalEnergyDeliveredKwh = Math.Round(totalEnergyDeliveredKwh, 2),
        });
    }
}
