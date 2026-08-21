using System.Text.Json;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Core.Charging;
using HASmartCharge.Backend.Services;
using HASmartCharge.Backend.Services.Telemetry;
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
    private readonly ICommandSender _commandSender;
    private readonly ZaptecService _zaptecService;

    public ChargerController(ApplicationDbContext dbContext, ChargerStatusTracker statusTracker, IChargerControl chargerControl, ChargeSessionRecorder sessionRecorder, ICommandSender commandSender, ZaptecService zaptecService)
    {
        _dbContext = dbContext;
        _statusTracker = statusTracker;
        _chargerControl = chargerControl;
        _sessionRecorder = sessionRecorder;
        _commandSender = commandSender;
        _zaptecService = zaptecService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (charger is null || string.IsNullOrWhiteSpace(charger.ActiveChargerId))
        {
            return Ok(new
            {
                chargePointId = charger?.ActiveChargerId ?? string.Empty,
                connected = false,
                connectorId = charger?.ConnectorId ?? 0,
                connectorStatus = (string?)null,
                currentPowerKw = (double?)null,
                sessionEnergyKwh = (double?)null,
                sessionCost = (decimal?)null,
                lastHeartbeatAt = (DateTime?)null
            });
        }

        var status = _statusTracker.GetChargerStatus(charger.ActiveChargerId);
        var connector = _statusTracker.GetConnectorStatus(charger.ActiveChargerId, charger.ConnectorId);
        var measurands = _statusTracker.GetConnectorMeasurands(charger.ActiveChargerId, charger.ConnectorId);

        // Session energy = current register minus the register captured at transaction start;
        // the raw register is a lifetime total, not per-session.
        double? sessionEnergyKwh = null;
        if (connector?.ActiveTransactionId is not null
            && connector.MeterStartKwh is { } meterStartKwh
            && measurands?.EnergyRegisterKwh is { } register)
        {
            sessionEnergyKwh = Math.Max(0, register - meterStartKwh);
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
            chargePointId = charger.ActiveChargerId,
            connected = status?.IsConnected ?? false,
            connectorId = charger.ConnectorId,
            connectorStatus = connector?.Status,
            currentPowerKw = measurands?.PowerKw,
            sessionEnergyKwh,
            sessionCost,
            lastHeartbeatAt = status?.LastHeartbeat
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
        if (charger is null || string.IsNullOrWhiteSpace(charger.ActiveChargerId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        // Clamp to the configured slider bounds so a stale/crafted request can't exceed them.
        var kw = Math.Clamp(request.Kw, charger.ChargePowerMinKw, charger.ChargePowerMaxKw);

        // UI works in kW; both OCPP charging profiles and Zaptec's maxChargeCurrent cap current, so
        // convert: A = W / (phases × voltage).
        var denominator = charger.PhaseCount * charger.SupplyVoltage;
        if (denominator <= 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity,
                new { error = "Charger PhaseCount and SupplyVoltage must be greater than zero to convert kW to amps" });
        }

        var amps = Math.Round(kw * 1000.0 / denominator, 1, MidpointRounding.ToZero);

        if (string.Equals(charger.ChargerType, ChargerTypes.Zaptec, StringComparison.OrdinalIgnoreCase))
        {
            var zaptecAmps = Math.Clamp(amps, 0, 32);
            try
            {
                await _zaptecService.SetMaxChargeCurrentAsync(zaptecAmps, ct);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
            }

            charger.ChargePowerSetpointKw = kw;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new
            {
                chargePointId = charger.ActiveChargerId,
                setpointKw = kw,
                amps = zaptecAmps,
                status = "Accepted",
                mode = "Zaptec",
                configurationKey = (string?)null,
                configurationValue = (string?)null
            });
        }

        var viaConfiguration = string.Equals(charger.ChargePowerControlMode,
            ChargePowerControlModes.Configuration, StringComparison.OrdinalIgnoreCase);

        OcppCommandResult result;
        string? sentValue = null;

        if (viaConfiguration)
        {
            if (string.IsNullOrWhiteSpace(charger.ChargePowerConfigurationKey))
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity,
                    new { error = "Charge power control mode is Configuration but no configuration key is set" });
            }

            if (!ChargePowerUnits.TryFormat(charger.ChargePowerConfigurationUnit, kw, amps, out sentValue))
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity,
                    new { error = $"Unsupported charge power configuration unit '{charger.ChargePowerConfigurationUnit}' (expected A, mA, W or kW)" });
            }

            result = await _chargerControl.SetConfigurationKeyAsync(
                charger.ChargePointId, charger.ChargePowerConfigurationKey, sentValue, ct);
        }
        else
        {
            result = await _chargerControl.SetChargingCurrentLimitAsync(
                charger.ChargePointId, charger.ConnectorId, amps, charger.PhaseCount, ct);
        }

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = result.ErrorDescription ?? result.ErrorCode ?? "Charger did not accept the command" });
        }

        // The .conf may still say Rejected/NotSupported even on a successful call.
        // ChangeConfiguration additionally has RebootRequired — the value did land, so accept it.
        var status = OcppValueHelpers.ReadStatus(result.RawPayload);
        var accepted = string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase)
            || (viaConfiguration && string.Equals(status, "RebootRequired", StringComparison.OrdinalIgnoreCase));
        if (!accepted)
        {
            var what = viaConfiguration ? $"configuration key {charger.ChargePowerConfigurationKey}" : "charging profile";
            return StatusCode(StatusCodes.Status422UnprocessableEntity,
                new { error = $"Charger rejected the {what} (status: {status ?? "unknown"})", status });
        }

        charger.ChargePowerSetpointKw = kw;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new
        {
            chargePointId = charger.ChargePointId,
            setpointKw = kw,
            amps,
            status,
            mode = viaConfiguration ? ChargePowerControlModes.Configuration : ChargePowerControlModes.ChargingProfile,
            configurationKey = viaConfiguration ? charger.ChargePowerConfigurationKey : null,
            configurationValue = sentValue
        });
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

    /// <summary>
    /// Developer/diagnostic escape hatch: sends an arbitrary OCPP action (including Reset and
    /// RemoteStopTransaction) straight to the charger, bypassing <see cref="IChargerControl"/>.
    /// </summary>
    [HttpPost("ocpp/call")]
    public async Task<IActionResult> SendOcppCall([FromBody] OcppCallRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new { error = "Action is required" });
        }

        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(charger?.ChargePointId))
        {
            return NotFound(new { error = "No charger configured" });
        }

        var payload = request.Payload ?? JsonDocument.Parse("{}").RootElement;
        var result = await _commandSender.SendCommandAsync(charger.ChargePointId, request.Action, payload, ct);
        return Ok(result);
    }

    public record SetAvailabilityRequest(bool Available);

    public record SetPowerRequest(double Kw);

    public record OcppCallRequest(string Action, JsonElement? Payload);
}
