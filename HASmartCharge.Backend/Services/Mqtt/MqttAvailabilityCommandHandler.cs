using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// Handles the <c>switch/operative/set</c> command from HA. Serialized by a semaphore, never throws.
/// The retained availability topic is only advisory, so this re-validates server-side with the same
/// shared <see cref="MqttSwitchRule"/> the publisher uses — a raw <c>mosquitto_pub</c> cannot bypass
/// it. Publishing is done through the caller-supplied closures (which also keep the publisher's diff
/// cache in sync), so there is still exactly one place that talks to the broker.
/// </summary>
public sealed class MqttAvailabilityCommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChargerStatusTracker _tracker;
    private readonly IChargerControl _control;
    private readonly ILogger<MqttAvailabilityCommandHandler> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MqttAvailabilityCommandHandler(
        IServiceScopeFactory scopeFactory,
        ChargerStatusTracker tracker,
        IChargerControl control,
        ILogger<MqttAvailabilityCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _control = control;
        _logger = logger;
    }

    /// <param name="publishState">Publish the switch state (true = ON/Operative), updating the cache.</param>
    /// <param name="publishAvailable">Publish the switch availability (true = online), updating the cache.</param>
    public async Task HandleAsync(string payload, Func<bool, Task> publishState, Func<bool, Task> publishAvailable, CancellationToken ct)
    {
        bool desiredOn;
        switch (payload?.Trim().ToUpperInvariant())
        {
            case "ON":
                desiredOn = true;
                break;
            case "OFF":
                desiredOn = false;
                break;
            default:
                _logger.LogDebug("Ignoring unparseable switch command payload '{Payload}'.", payload);
                return;
        }

        try
        {
            await _gate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var charger = await db.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);

            string? connectorStatus = null;
            var isConnected = false;
            if (charger is not null && !string.IsNullOrWhiteSpace(charger.ChargePointId))
            {
                isConnected = _tracker.GetChargerStatus(charger.ChargePointId)?.IsConnected ?? false;
                connectorStatus = _tracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId)?.Status;
            }

            // Snap-back = republish the true current state + availability, which visually bounces HA's
            // toggle back to reality.
            async Task SnapBackAsync()
            {
                await publishState(MqttSwitchRule.IsOn(connectorStatus));
                await publishAvailable(MqttSwitchRule.IsAvailable(isConnected, connectorStatus));
            }

            if (charger is null || string.IsNullOrWhiteSpace(charger.ChargePointId))
            {
                _logger.LogDebug("Switch command received but no charger is configured; snapping back.");
                await SnapBackAsync();
                return;
            }

            if (!MqttSwitchRule.CanApply(isConnected, connectorStatus, desiredOn))
            {
                _logger.LogInformation(
                    "Switch command {Desired} not allowed (connected={Connected}, status={Status}); snapping back.",
                    desiredOn ? "ON" : "OFF", isConnected, connectorStatus ?? "null");
                await SnapBackAsync();
                return;
            }

            var result = await _control.SetConnectorAvailabilityAsync(charger.ChargePointId, charger.ConnectorId, desiredOn, ct);
            var status = OcppValueHelpers.ReadStatus(result.RawPayload);

            if (result.Success && string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
            {
                // Optimistic: the charger's follow-up StatusNotification nudges the loop, which then
                // reconciles to the confirmed state (and flips availability if needed).
                await publishState(desiredOn);
            }
            else if (result.Success && string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                // OCPP 1.6 legal: the charger will apply it later. Don't flip now; the eventual
                // StatusNotification drives the real state via the nudge — no pending bookkeeping.
                _logger.LogInformation("Availability change was Scheduled by the charger; leaving state until it reports back.");
                await SnapBackAsync();
            }
            else
            {
                _logger.LogWarning(
                    "Availability change failed (success={Success}, status={Status}, error={Error}); snapping back.",
                    result.Success, status ?? "null", result.ErrorDescription ?? result.ErrorCode ?? "none");
                await SnapBackAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT availability command.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
