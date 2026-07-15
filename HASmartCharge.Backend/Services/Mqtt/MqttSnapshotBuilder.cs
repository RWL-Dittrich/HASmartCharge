using System.Globalization;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// One immutable snapshot of everything the publisher pushes. Every sensor field is a pre-formatted
/// MQTT payload string; an unknown sensor value is the literal <c>"None"</c>
/// (<see cref="MqttSnapshotBuilder.Unknown"/>), which is HA's <c>PAYLOAD_NONE</c> — HA checks it
/// BEFORE the numeric/enum/timestamp branches and cleanly sets the entity to "unknown". An empty
/// payload does NOT work: HA ignores it and keeps the previous value. Switch/binary payloads
/// (ON/OFF, online/offline) are never unknown. <see cref="Currency"/>/<see cref="ChargePointId"/>
/// are discovery-config inputs rather than state values, carried here from the same DB read.
/// </summary>
public record MqttSnapshot(
    string PowerKw,
    string CarSoc,
    string Connected,
    string ConnectorStatus,
    string SessionEnergyKwh,
    string SessionCost,
    string LastHeartbeat,
    string PlanDeadline,
    string PlanTargetSoc,
    string PlanRequiredKwh,
    string PlanEstimatedCost,
    string SwitchState,
    string SwitchAvailable,
    string Currency,
    string ChargePointId);

/// <summary>
/// Aggregates live charger status (OCPP tracker), live cost (session recorder), the active plan and
/// currency (DB), and the car SoC (HA, 30s-cached) into an <see cref="MqttSnapshot"/>. Singleton
/// with a fresh DI scope per call, since <c>ApplicationDbContext</c> and <c>IHomeAssistantControl</c>
/// are scoped.
/// </summary>
public sealed class MqttSnapshotBuilder
{
    /// <summary>HA's PAYLOAD_NONE — the payload that sets any sensor entity to "unknown".</summary>
    internal const string Unknown = "None";

    private static readonly TimeSpan _socCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChargerStatusTracker _tracker;
    private readonly ChargeSessionRecorder _recorder;
    private readonly ILogger<MqttSnapshotBuilder> _logger;

    // SoC is an HTTP round-trip to HA; cache it so a 10s publish tick doesn't hammer HA. Only ever
    // touched from the single publisher loop, so no locking is needed.
    private double? _cachedSoc;
    private DateTime? _socCachedAtUtc;

    public MqttSnapshotBuilder(
        IServiceScopeFactory scopeFactory,
        ChargerStatusTracker tracker,
        ChargeSessionRecorder recorder,
        ILogger<MqttSnapshotBuilder> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _recorder = recorder;
        _logger = logger;
    }

    public async Task<MqttSnapshot> BuildAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var charger = await db.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var car = await db.CarSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var price = await db.PriceProviderSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var currency = price?.Currency ?? "EUR";

        var plan = await db.ChargePlans
            .AsNoTracking()
            .Where(p => p.Status == ChargePlanStatus.Pending || p.Status == ChargePlanStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        ChargerStatus? status = null;
        ConnectorStatus? connector = null;
        ConnectorMeasurands? measurands = null;
        if (charger is not null && !string.IsNullOrWhiteSpace(charger.ChargePointId))
        {
            status = _tracker.GetChargerStatus(charger.ChargePointId);
            connector = _tracker.GetConnectorStatus(charger.ChargePointId, charger.ConnectorId);
            measurands = _tracker.GetConnectorMeasurands(charger.ChargePointId, charger.ConnectorId);
        }

        var isConnected = status?.IsConnected ?? false;

        // Charger-derived values are only meaningful while connected; otherwise the tracker holds
        // stale readings, so publish them as unknown.
        var powerKw = isConnected ? OcppValueHelpers.ToKw(measurands?.PowerActiveImport) : null;
        var connectorStatusStr = isConnected ? connector?.Status ?? Unknown : Unknown;

        double? sessionEnergyKwh = null;
        decimal? sessionCost = null;
        if (isConnected && connector?.ActiveTransactionId is { } txId)
        {
            if (connector.MeterStartKwh is { } meterStartKwh
                && measurands?.EnergyActiveImportRegister?.AsDecimal() is { } register)
            {
                sessionEnergyKwh = Math.Max(0, (double)register - meterStartKwh);
            }

            var liveCost = await _recorder.TryGetLiveCostAsync(txId, ct);
            sessionCost = liveCost?.TotalCost; // null mid-session → unknown, not 0
        }

        var carSoc = await ResolveSocAsync(scope, car, isConnected, measurands, ct);

        var switchOn = MqttSwitchRule.IsOn(connector?.Status);
        var switchAvailable = MqttSwitchRule.IsAvailable(isConnected, connector?.Status);

        return new MqttSnapshot(
            PowerKw: Num(powerKw),
            CarSoc: Num(carSoc),
            Connected: isConnected ? "ON" : "OFF",
            ConnectorStatus: connectorStatusStr,
            SessionEnergyKwh: Num(sessionEnergyKwh),
            SessionCost: Num(sessionCost),
            LastHeartbeat: Timestamp(status?.LastHeartbeat),
            PlanDeadline: Timestamp(plan?.DeadlineUtc),
            PlanTargetSoc: plan is null ? Unknown : plan.TargetSocPercent.ToString(CultureInfo.InvariantCulture),
            PlanRequiredKwh: plan is null ? Unknown : Num(plan.EstimatedEnergyKwh),
            PlanEstimatedCost: plan is null ? Unknown : Num(plan.EstimatedCost),
            SwitchState: switchOn ? "ON" : "OFF",
            SwitchAvailable: switchAvailable ? "online" : "offline",
            Currency: currency,
            ChargePointId: charger?.ChargePointId ?? "");
    }

    private async Task<double?> ResolveSocAsync(IServiceScope scope, CarSettings? car, bool isConnected, ConnectorMeasurands? measurands, CancellationToken ct)
    {
        // Primary: an HA SoC entity (independent of the charger link), cached for 30s.
        if (!string.IsNullOrWhiteSpace(car?.HaSocEntityId))
        {
            var now = DateTime.UtcNow;
            if (_socCachedAtUtc is null || now - _socCachedAtUtc.Value >= _socCacheTtl)
            {
                try
                {
                    var ha = scope.ServiceProvider.GetRequiredService<IHomeAssistantControl>();
                    _cachedSoc = await ha.GetBatterySocAsync(car.HaSocEntityId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to read car SoC from Home Assistant entity {EntityId}.", car.HaSocEntityId);
                }

                _socCachedAtUtc = now;
            }

            return _cachedSoc;
        }

        // Fallback: OCPP SoC measurand, only meaningful while connected.
        return isConnected && measurands?.SoC?.AsDecimal() is { } soc ? (double)soc : null;
    }

    private static string Num(double? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : Unknown;

    private static string Num(decimal? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : Unknown;

    private static string Timestamp(DateTime? value)
    {
        if (value is not { } v)
        {
            return Unknown;
        }

        // SQLite round-trips DateTime as Kind=Unspecified; re-stamp Utc so the payload carries "Z".
        var utc = v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };
        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
