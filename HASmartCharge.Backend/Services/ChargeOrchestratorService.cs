using System.Text.Json;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Ticks every ~60s: for the active "full by deadline" plan, recomputes the cheapest-hour
/// schedule against the live SoC/prices and toggles the car's HA start/stop service on
/// selected-hour transitions. See plan.md §7.
/// </summary>
public class ChargeOrchestratorService : BackgroundService
{
    private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(60);

    private static readonly ChargePlanStatus[] _relevantStatuses =
    [
        ChargePlanStatus.Pending, ChargePlanStatus.Active, ChargePlanStatus.MissedDeadline
    ];

    // OCPP connector statuses that mean a cable is inserted (as opposed to Available/Unavailable/Faulted).
    private static readonly HashSet<string> _pluggedInStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Preparing", "Charging", "SuspendedEV", "SuspendedEVSE", "Finishing"
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ManualOverrideState _overrideState;
    private readonly PlugStateTracker _plugState;
    private readonly ILogger<ChargeOrchestratorService> _logger;

    public ChargeOrchestratorService(
        IServiceProvider serviceProvider,
        ManualOverrideState overrideState,
        PlugStateTracker plugState,
        ILogger<ChargeOrchestratorService> logger)
    {
        _serviceProvider = serviceProvider;
        _overrideState = overrideState;
        _plugState = plugState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Charge orchestrator service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in charge orchestrator tick.");
            }

            try
            {
                await Task.Delay(_tickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Charge orchestrator service stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var haControl = services.GetRequiredService<IHomeAssistantControl>();
        var scheduleService = services.GetRequiredService<IPlanScheduleService>();
        var chargeControl = services.GetRequiredService<IChargeControlService>();
        var statusTracker = services.GetRequiredService<ChargerStatusTracker>();

        // Auto-schedule: drop past overrides, then create a plan on the plug-in rising edge
        // before we look for an active one.
        await services.GetRequiredService<IAutoScheduleResolver>().SweepPastOverridesAsync(DateTime.UtcNow, ct);
        await TryAutoArmAsync(services, dbContext, statusTracker, haControl, ct);

        var plan = await dbContext.ChargePlans
            .Where(p => _relevantStatuses.Contains(p.Status))
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (plan is null)
        {
            _logger.LogDebug("Charge orchestrator tick: no active plan, idling.");
            return;
        }

        if (plan.Status == ChargePlanStatus.Pending)
        {
            plan.Status = ChargePlanStatus.Active;
            await dbContext.SaveChangesAsync(ct);
        }

        if (_overrideState.IsActive)
        {
            _logger.LogInformation(
                "Charge orchestrator tick: manual override active until {OverrideUntilUtc:o}, skipping automatic control.",
                _overrideState.OverrideUntilUtc);
            return;
        }

        var car = await dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        var charger = await dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(car.HaSocEntityId))
        {
            _logger.LogWarning("Charge orchestrator tick: no car SoC entity configured, skipping tick.");
            return;
        }

        var soc = await haControl.GetBatterySocAsync(car.HaSocEntityId, ct);
        if (soc is null)
        {
            _logger.LogWarning("Charge orchestrator tick: battery SoC unavailable, skipping tick.");
            return;
        }

        var isCharging = await IsChargingAsync(statusTracker, haControl, charger, car, ct);

        if (soc.Value >= plan.TargetSocPercent)
        {
            if (isCharging)
            {
                await TryStopChargingAsync(chargeControl, plan.Id, ct);
            }

            plan.Status = ChargePlanStatus.Completed;
            plan.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Charge orchestrator tick: plan {PlanId} completed, SoC {Soc}% reached target {Target}%.",
                plan.Id, soc, plan.TargetSocPercent);
            return;
        }

        var now = DateTime.UtcNow;
        var calc = await scheduleService.ComputeAsync(plan.DeadlineUtc, plan.TargetSocPercent, soc.Value, ct);

        plan.SelectedHoursJson = JsonSerializer.Serialize(calc.Schedule.SelectedHourStartsUtc);
        plan.EstimatedCost = calc.Schedule.EstimatedCost;
        plan.EstimatedEnergyKwh = calc.Schedule.EnergyNeededKwh;

        var nowHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var shouldCharge = calc.Schedule.SelectedHourStartsUtc.Contains(nowHour);
        var acted = false;

        if (shouldCharge && !isCharging)
        {
            acted = await TryStartChargingAsync(chargeControl, plan.Id, ct);
        }
        else if (!shouldCharge && isCharging)
        {
            acted = await TryStopChargingAsync(chargeControl, plan.Id, ct);
        }

        if (now > plan.DeadlineUtc && soc.Value < plan.TargetSocPercent)
        {
            if (plan.Status != ChargePlanStatus.MissedDeadline)
            {
                _logger.LogWarning(
                    "Charge plan {PlanId} missed its deadline {DeadlineUtc:o} at {Soc}% (target {Target}%); continuing to charge toward target.",
                    plan.Id, plan.DeadlineUtc, soc, plan.TargetSocPercent);
            }

            plan.Status = ChargePlanStatus.MissedDeadline;
        }

        await dbContext.SaveChangesAsync(ct);

        if (!acted)
        {
            _logger.LogDebug(
                "Charge orchestrator tick: plan {PlanId}, SoC {Soc}%, shouldCharge={ShouldCharge}, isCharging={IsCharging}, no transition.",
                plan.Id, soc, shouldCharge, isCharging);
        }
    }

    /// <summary>
    /// When auto-scheduling is enabled, detects the plug-in rising edge and, if no plan is already
    /// in flight, creates one for the next departure resolved from the weekly pattern + overrides.
    /// </summary>
    private async Task TryAutoArmAsync(
        IServiceProvider services,
        ApplicationDbContext dbContext,
        ChargerStatusTracker statusTracker,
        IHomeAssistantControl haControl,
        CancellationToken ct)
    {
        var auto = await dbContext.AutoScheduleSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (auto is null || !auto.Enabled)
        {
            return;
        }

        var charger = await dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);
        var car = await dbContext.CarSettings.AsNoTracking().FirstAsync(ct);

        var pluggedNow = await IsPluggedInAsync(statusTracker, haControl, charger, car, ct);
        if (!_plugState.RegisterAndDetectRisingEdge(pluggedNow))
        {
            return;
        }

        var hasPlan = await dbContext.ChargePlans.AnyAsync(p => _relevantStatuses.Contains(p.Status), ct);
        if (hasPlan)
        {
            _logger.LogInformation("Auto-schedule: car plugged in but a plan is already active; not creating another.");
            return;
        }

        var resolver = services.GetRequiredService<IAutoScheduleResolver>();
        var next = await resolver.ResolveNextDepartureAsync(DateTime.UtcNow, ct);
        if (next is null)
        {
            _logger.LogInformation("Auto-schedule: car plugged in but no upcoming departure configured; nothing to arm.");
            return;
        }

        var factory = services.GetRequiredService<IChargePlanFactory>();
        var plan = await factory.CreateAsync(next.DeadlineUtc, next.TargetSocPercent, ct);
        _logger.LogInformation(
            "Auto-schedule: car plugged in, created plan {PlanId} with deadline {DeadlineUtc:o}, target {Target}%.",
            plan.Id, next.DeadlineUtc, plan.TargetSocPercent);
    }

    /// <summary>
    /// Is the car plugged in? Primary signal is the OCPP connector status; if a HA plugged-in
    /// binary_sensor is configured, it's OR'd in.
    /// </summary>
    private static async Task<bool> IsPluggedInAsync(
        ChargerStatusTracker statusTracker, IHomeAssistantControl haControl, ChargerSettings charger, CarSettings car, CancellationToken ct)
    {
        var connector = statusTracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId);
        if (connector?.Status is { } status && _pluggedInStatuses.Contains(status))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(car.HaPluggedInEntityId))
        {
            return false;
        }

        var haState = await haControl.GetStateAsync(car.HaPluggedInEntityId, ct);
        return haState is not null && (
            haState.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            haState.Equals("plugged", StringComparison.OrdinalIgnoreCase) ||
            haState.Equals("connected", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Primary signal is the OCPP connector status; if a HA charging-state entity is configured,
    /// it's OR'd in (either source reporting "charging" counts).
    /// </summary>
    private static async Task<bool> IsChargingAsync(
        ChargerStatusTracker statusTracker, IHomeAssistantControl haControl, ChargerSettings charger, CarSettings car, CancellationToken ct)
    {
        var connector = statusTracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId);
        var ocppCharging = string.Equals(connector?.Status, "Charging", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(car.HaChargingStateEntityId))
        {
            return ocppCharging;
        }

        var haState = await haControl.GetStateAsync(car.HaChargingStateEntityId, ct);
        var haCharging = haState is not null &&
            (haState.Equals("on", StringComparison.OrdinalIgnoreCase) || haState.Equals("charging", StringComparison.OrdinalIgnoreCase));

        return ocppCharging || haCharging;
    }

    private async Task<bool> TryStartChargingAsync(IChargeControlService chargeControl, int planId, CancellationToken ct)
    {
        try
        {
            await chargeControl.StartChargingAsync(ct);
            _logger.LogInformation("Charge orchestrator: started charging for plan {PlanId}.", planId);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Charge orchestrator: failed to start charging for plan {PlanId}.", planId);
            return false;
        }
    }

    private async Task<bool> TryStopChargingAsync(IChargeControlService chargeControl, int planId, CancellationToken ct)
    {
        try
        {
            await chargeControl.StopChargingAsync(ct);
            _logger.LogInformation("Charge orchestrator: stopped charging for plan {PlanId}.", planId);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Charge orchestrator: failed to stop charging for plan {PlanId}.", planId);
            return false;
        }
    }
}
