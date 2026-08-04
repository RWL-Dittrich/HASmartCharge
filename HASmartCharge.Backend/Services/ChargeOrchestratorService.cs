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

        var car = await dbContext.CarSettings.AsNoTracking().FirstAsync(ct);
        var charger = await dbContext.ChargerSettings.AsNoTracking().FirstAsync(ct);

        // Resolve the at-home latch once per tick: everything below (auto-arm + control) reads it.
        var atHome = await ResolveAtHomeAsync(statusTracker, haControl, charger, car, ct);

        // Auto-schedule: drop past overrides, then create a plan on the plug-in rising edge
        // before we look for an active one.
        await services.GetRequiredService<IAutoScheduleResolver>().SweepPastOverridesAsync(DateTime.UtcNow, ct);
        if (atHome.RisingEdge)
        {
            await TryAutoArmAsync(services, dbContext, ct);
        }

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

        // Only ever touch the car while it's on our charger. Without this the plan keeps running
        // after the car leaves and we'd start/stop it at whatever public charger it's plugged into.
        if (!atHome.IsAtHome)
        {
            _logger.LogInformation(
                "Charge orchestrator tick: plan {PlanId} but car is not plugged into charger {ChargePointId}; skipping automatic control.",
                plan.Id, charger.ChargePointId);
            return;
        }

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
    /// Called on the at-home plug-in rising edge when auto-scheduling is enabled: retires any plan
    /// whose deadline has already passed, and — if no plan for an upcoming departure is still in
    /// flight — creates one for the next departure resolved from the weekly pattern + overrides.
    /// </summary>
    private async Task TryAutoArmAsync(
        IServiceProvider services,
        ApplicationDbContext dbContext,
        CancellationToken ct)
    {
        var auto = await dbContext.AutoScheduleSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (auto is null || !auto.Enabled)
        {
            return;
        }

        // Retire plans whose deadline is already behind us. Nothing else moves a plan out of
        // MissedDeadline, so without this a single missed departure (target not reached in time,
        // or the car unplugged mid-charge) leaves a relevant plan stuck forever and blocks every
        // future auto-arm. A fresh plug-in is always for an upcoming departure, so a past-deadline
        // plan is dead — only a plan whose deadline is still ahead counts as "already armed".
        var now = DateTime.UtcNow;
        var relevantPlans = await dbContext.ChargePlans
            .Where(p => _relevantStatuses.Contains(p.Status))
            .ToListAsync(ct);

        var stale = relevantPlans.Where(p => p.DeadlineUtc <= now).ToList();
        foreach (var expired in stale)
        {
            expired.Status = ChargePlanStatus.Cancelled;
            expired.CompletedAt = now;
        }

        if (stale.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Auto-schedule: cancelled {Count} stale plan(s) past their deadline.", stale.Count);
        }

        if (relevantPlans.Any(p => p.DeadlineUtc > now))
        {
            _logger.LogInformation("Auto-schedule: car plugged in but a plan for an upcoming departure is already active; not creating another.");
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
    /// Resolves whether this charge is an at-home charge, feeding the latch in
    /// <see cref="PlugStateTracker"/>.
    /// <para>
    /// Confirmation (both signals at once) requires the home charger to be online — a stale
    /// connector status from a disconnected charger proves nothing — reporting a cable inserted,
    /// AND, when configured, the car-side HA plugged sensor agreeing. That sensor can't tell which
    /// charger the cable is in, so it is never OR'd: at a public charger it would alone read
    /// "plugged" and we'd start/stop the car remotely.
    /// </para>
    /// Detachment must be positive, not merely unknown: the charger is online and its connector
    /// reports no cable, or the HA sensor says the car is unplugged. A charger that dropped its
    /// WebSocket mid-charge is ambiguous, so the latch holds and we keep control.
    /// </summary>
    private async Task<(bool IsAtHome, bool RisingEdge)> ResolveAtHomeAsync(
        ChargerStatusTracker statusTracker,
        IHomeAssistantControl haControl,
        ChargerSettings charger,
        CarSettings car,
        CancellationToken ct)
    {
        var chargerOnline = statusTracker.GetChargerStatus(charger.ChargePointId)?.IsConnected == true;
        var connectorPlugged = statusTracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId)?.Status is { } status
            && _pluggedInStatuses.Contains(status);

        var carPlugged = string.IsNullOrWhiteSpace(car.HaPluggedInEntityId)
            ? null
            : await IsCarPluggedAsync(haControl, car.HaPluggedInEntityId, ct);

        var confirmed = chargerOnline && connectorPlugged && carPlugged != false;
        var detached = (chargerOnline && !connectorPlugged) || carPlugged == false;

        var rising = _plugState.RegisterAndDetectRisingEdge(confirmed, detached);
        var isAtHome = _plugState.IsAtHome;

        _logger.LogDebug(
            "Plug state: chargerOnline={ChargerOnline}, connectorPlugged={ConnectorPlugged}, carPlugged={CarPlugged}, confirmed={Confirmed}, detached={Detached}, atHome={AtHome}.",
            chargerOnline, connectorPlugged, carPlugged, confirmed, detached, isAtHome);

        return (isAtHome, rising);
    }

    private static async Task<bool?> IsCarPluggedAsync(IHomeAssistantControl haControl, string entityId, CancellationToken ct)
    {
        var haState = await haControl.GetStateAsync(entityId, ct);
        if (haState is null || haState.Equals("unavailable", StringComparison.OrdinalIgnoreCase)
            || haState.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null; // unreachable/unknown entity is ambiguous, not a detach.
        }

        return haState.Equals("on", StringComparison.OrdinalIgnoreCase)
            || haState.Equals("plugged", StringComparison.OrdinalIgnoreCase)
            || haState.Equals("connected", StringComparison.OrdinalIgnoreCase);
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
