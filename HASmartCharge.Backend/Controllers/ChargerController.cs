using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Live charger status (from the in-memory OCPP tracker) and outbound charger commands
/// (unlock, availability, re-push config).
/// </summary>
[ApiController]
[Route("api/charger")]
public class ChargerController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ChargerStatusTracker _statusTracker;
    private readonly IChargerControl _chargerControl;
    private readonly ChargeSessionRecorder _sessionRecorder;

    public ChargerController(ApplicationDbContext dbContext, ChargerStatusTracker statusTracker, IChargerControl chargerControl, ChargeSessionRecorder sessionRecorder)
    {
        _dbContext = dbContext;
        _statusTracker = statusTracker;
        _chargerControl = chargerControl;
        _sessionRecorder = sessionRecorder;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (charger is null || string.IsNullOrWhiteSpace(charger.ChargePointId))
        {
            return Ok(new
            {
                chargePointId = charger?.ChargePointId ?? string.Empty,
                connected = false,
                connectorId = charger?.ConnectorId ?? 0,
                connectorStatus = (string?)null,
                currentPowerKw = (double?)null,
                sessionEnergyKwh = (double?)null,
                sessionCost = (decimal?)null,
                lastHeartbeatAt = (DateTime?)null
            });
        }

        var status = _statusTracker.GetChargerStatus(charger.ChargePointId);
        var connector = _statusTracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId);
        var measurands = _statusTracker.GetConnectorMeasurands(charger.ChargePointId, charger.ConnectorId);

        // Session energy = current register minus the register captured at transaction start;
        // the raw register is a lifetime total, not per-session.
        double? sessionEnergyKwh = null;
        if (connector?.ActiveTransactionId is not null
            && connector.MeterStartKwh is { } meterStartKwh
            && measurands?.EnergyActiveImportRegister?.AsDecimal() is { } register)
        {
            sessionEnergyKwh = Math.Max(0, (double)register - meterStartKwh);
        }

        // Live cost so far for the in-progress transaction (null when idle).
        decimal? sessionCost = null;
        if (connector?.ActiveTransactionId is { } txId)
        {
            var liveCost = await _sessionRecorder.TryGetLiveCostAsync(txId, ct);
            sessionCost = liveCost?.TotalCost;
        }

        return Ok(new
        {
            chargePointId = charger.ChargePointId,
            connected = status?.IsConnected ?? false,
            connectorId = charger.ConnectorId,
            connectorStatus = connector?.Status,
            currentPowerKw = ToKw(measurands?.PowerActiveImport),
            sessionEnergyKwh,
            sessionCost,
            lastHeartbeatAt = status?.LastUpdated
        });
    }

    [HttpPost("unlock")]
    public async Task<IActionResult> Unlock(CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(charger?.ChargePointId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        var result = await _chargerControl.UnlockConnectorAsync(charger.ChargePointId, charger.ConnectorId, ct);
        return Ok(result);
    }

    [HttpPost("availability")]
    public async Task<IActionResult> SetAvailability([FromBody] SetAvailabilityRequest request, CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(charger?.ChargePointId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        var result = await _chargerControl.SetConnectorAvailabilityAsync(charger.ChargePointId, charger.ConnectorId, request.Available, ct);
        return Ok(result);
    }

    [HttpPost("power")]
    public async Task<IActionResult> SetPower([FromBody] SetPowerRequest request, CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.FirstOrDefaultAsync(ct);
        if (charger is null || string.IsNullOrWhiteSpace(charger.ChargePointId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        // Clamp to the configured slider bounds so a stale/crafted request can't exceed them.
        var kw = Math.Clamp(request.Kw, charger.ChargePowerMinKw, charger.ChargePowerMaxKw);

        // UI works in kW; OCPP charging profiles cap current, so convert: A = W / (phases × voltage).
        var denominator = charger.PhaseCount * charger.SupplyVoltage;
        if (denominator <= 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity,
                new { error = "Charger PhaseCount and SupplyVoltage must be greater than zero to convert kW to amps" });
        }

        var amps = Math.Round(kw * 1000.0 / denominator, 1, MidpointRounding.ToZero);

        var result = await _chargerControl.SetChargingCurrentLimitAsync(
            charger.ChargePointId, charger.ConnectorId, amps, charger.PhaseCount, ct);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = result.ErrorDescription ?? result.ErrorCode ?? "Charger did not accept the command" });
        }

        // SetChargingProfile.conf may still say Rejected/NotSupported even on a successful call.
        var status = ReadStatus(result.RawPayload);
        if (!string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity,
                new { error = $"Charger rejected the charging profile (status: {status ?? "unknown"})", status });
        }

        charger.ChargePowerSetpointKw = kw;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { chargePointId = charger.ChargePointId, setpointKw = kw, amps, status });
    }

    [HttpPost("reconfigure")]
    public async Task<IActionResult> Reconfigure(CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(charger?.ChargePointId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        await _chargerControl.ReconfigureAsync(charger.ChargePointId, ct);
        return Ok(new { chargePointId = charger.ChargePointId, reconfigured = true });
    }

    /// <summary>OCPP's default unit for Power.Active.Import is watts; convert to kW unless the charger already reports kW.</summary>
    private static double? ToKw(MeasurandValue? value)
    {
        if (value?.AsDecimal() is not { } raw)
        {
            return null;
        }

        var kw = (double)raw;
        if (!string.Equals(value.Unit, "kW", StringComparison.OrdinalIgnoreCase))
        {
            kw /= 1000;
        }

        return kw;
    }

    /// <summary>Reads the "status" string from an OCPP CALLRESULT payload, if present.</summary>
    private static string? ReadStatus(System.Text.Json.JsonElement? payload)
    {
        if (payload is { } el
            && el.ValueKind == System.Text.Json.JsonValueKind.Object
            && el.TryGetProperty("status", out var statusEl)
            && statusEl.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return statusEl.GetString();
        }

        return null;
    }

    public record SetAvailabilityRequest(bool Available);

    public record SetPowerRequest(double Kw);
}
