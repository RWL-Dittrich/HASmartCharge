using HASmartCharge.Application.Interfaces;
using HASmartCharge.Application.Queries.Models;
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
    private static readonly string[] _knownConnectorStatuses =
    [
        "Available",
        "Preparing",
        "Charging",
        "SuspendedEVSE",
        "SuspendedEV",
        "Finishing",
        "Reserved",
        "Unavailable",
        "Faulted"
    ];

    private readonly IChargerReadModel _chargerReadModel;

    public DashboardController(IChargerReadModel chargerReadModel)
    {
        _chargerReadModel = chargerReadModel;
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
        List<ChargerSnapshot> all = _chargerReadModel.GetChargers().ToList();

        int totalChargers = all.Count;
        int onlineChargers = all.Count(s => s.IsConnected);

        // Flatten every connector across all chargers
        List<ConnectorSnapshot> allConnectors = all
            .SelectMany(s => s.Connectors)
            .ToList();

        // Count connectors by OCPP status
        Dictionary<string, int> connectorsByStatus = _knownConnectorStatuses
            .Concat(["Unknown"])
            .ToDictionary(status => status, _ => 0);

        foreach (ConnectorSnapshot c in allConnectors)
        {
            string key = connectorsByStatus.ContainsKey(c.Status) ? c.Status : "Unknown";
            connectorsByStatus[key]++;
        }

        // Active transactions with live measurands
        var activeTransactions = _chargerReadModel.GetActiveChargingSessions()
            .OrderByDescending(t => t.StartedAt)
            .Select(t => new
            {
                chargePointId = t.ChargerId,
                connectorId = t.ConnectorId,
                transactionId = t.SessionId,
                idTag = t.AuthorizationTag,
                startTime = t.StartedAt,
                connectorStatus = t.ConnectorStatus,
                energyActiveImportWh = MapMeasurementValue(t.Measurements?.ImportedEnergy),
                powerActiveImport = MapMeasurementValue(t.Measurements?.ImportedPower),
            })
            .ToList();

        // Fleet-wide power & energy totals from all connector measurands
        decimal totalPowerDrawKw = 0;
        decimal totalEnergyDeliveredKwh = 0;

        foreach (ChargerSnapshot charger in all)
        {
            foreach (ConnectorMeasurementsSnapshot measurements in charger.Connectors
                         .Select(connector => connector.Measurements)
                         .OfType<ConnectorMeasurementsSnapshot>())
            {
                if (measurements.ImportedPower?.AsDecimal() is { } power)
                {
                    string unit = measurements.ImportedPower.Unit ?? "W";
                    totalPowerDrawKw += unit.Equals("kW", StringComparison.OrdinalIgnoreCase)
                        ? power
                        : power / 1000m;
                }

                if (measurements.ImportedEnergy?.AsDecimal() is { } energy)
                {
                    string unit = measurements.ImportedEnergy.Unit ?? "Wh";
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

    private static object? MapMeasurementValue(MeasurementValueSnapshot? value)
    {
        return value is null
            ? null
            : new
            {
                value = value.Value,
                unit = value.Unit,
                context = value.Context,
                format = value.Format,
                location = value.Location,
                phase = value.Phase,
                timestamp = value.Timestamp
            };
    }
}
