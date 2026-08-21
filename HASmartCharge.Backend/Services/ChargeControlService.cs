using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.Services.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

public class ChargeControlService : IChargeControlService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHomeAssistantControl _haControl;
    private readonly IChargerControl _chargerControl;
    private readonly ChargerStatusTracker _statusTracker;
    private readonly ZaptecService _zaptecService;
    private readonly ILogger<ChargeControlService> _logger;

    public ChargeControlService(
        ApplicationDbContext dbContext,
        IHomeAssistantControl haControl,
        IChargerControl chargerControl,
        ChargerStatusTracker statusTracker,
        ZaptecService zaptecService,
        ILogger<ChargeControlService> logger)
    {
        _dbContext = dbContext;
        _haControl = haControl;
        _chargerControl = chargerControl;
        _statusTracker = statusTracker;
        _zaptecService = zaptecService;
        _logger = logger;
    }

    public async Task StartChargingAsync(CancellationToken ct = default)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);

        if (!string.Equals(car.ChargeControlMode, ChargeControlModes.Charger, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(car.HaStartDomain) || string.IsNullOrWhiteSpace(car.HaStartService))
            {
                throw new InvalidOperationException("Car start service not configured");
            }

            await _haControl.CallServiceAsync(car.HaStartDomain, car.HaStartService, car.HaStartDataJson, ct);
            return;
        }

        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);
        if (string.Equals(charger.ChargerType, ChargerTypes.Zaptec, StringComparison.OrdinalIgnoreCase))
        {
            // Car ended the session itself (opMode 5, no FinalStopActive) — resuming would just
            // provoke a 507 rejection every orchestrator tick, so treat it as a no-op.
            if (_zaptecService.OperationMode == 5 && !_zaptecService.FinalStopActive)
            {
                _logger.LogInformation("Zaptec charger finished its session on its own; not resuming.");
                return;
            }

            // opMode 1 (disconnected) or 2 (requesting) — no session for pause/resume to steer yet.
            if (_zaptecService.OperationMode is 1 or 2)
            {
                _logger.LogInformation("Zaptec charger has no active session yet (opMode {OperationMode}); not resuming.", _zaptecService.OperationMode);
                return;
            }

            await _zaptecService.ResumeAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(charger.ChargePointId))
        {
            throw new InvalidOperationException("No OCPP charger configured");
        }

        var result = await _chargerControl.RemoteStartTransactionAsync(charger.ChargePointId, charger.ConnectorId, ct);
        EnsureAccepted(result, "RemoteStartTransaction");
    }

    public async Task StopChargingAsync(CancellationToken ct = default)
    {
        var car = await _dbContext.CarSettings.AsNoTracking().FirstAsync(ct);

        if (!string.Equals(car.ChargeControlMode, ChargeControlModes.Charger, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(car.HaStopDomain) || string.IsNullOrWhiteSpace(car.HaStopService))
            {
                throw new InvalidOperationException("Car stop service not configured");
            }

            await _haControl.CallServiceAsync(car.HaStopDomain, car.HaStopService, car.HaStopDataJson, ct);
            return;
        }

        var charger = await _dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);
        if (string.Equals(charger.ChargerType, ChargerTypes.Zaptec, StringComparison.OrdinalIgnoreCase))
        {
            // 506 is Zaptec's documented pause — resumable via 507, unlike a hard stop.
            await _zaptecService.PauseAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(charger.ChargePointId))
        {
            throw new InvalidOperationException("No OCPP charger configured");
        }

        var connector = _statusTracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId);
        if (connector?.ActiveTransactionId is not { } transactionId)
        {
            throw new InvalidOperationException("No active transaction to stop");
        }

        var result = await _chargerControl.RemoteStopTransactionAsync(charger.ChargePointId, transactionId, ct);
        EnsureAccepted(result, "RemoteStopTransaction");
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> unless the command succeeded AND the
    /// charger's .conf status was Accepted.</summary>
    private static void EnsureAccepted(OcppCommandResult result, string action)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorDescription ?? result.ErrorCode ?? $"{action} failed");
        }

        var status = OcppValueHelpers.ReadStatus(result.RawPayload);
        if (!string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Charger rejected {action} (status: {status ?? "unknown"})");
        }
    }
}
