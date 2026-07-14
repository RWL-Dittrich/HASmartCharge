using System.Collections.Concurrent;
using System.Threading.Channels;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// The single background loop that publishes charging telemetry to Home Assistant over MQTT and
/// serves the switch command. Follows the repo loop convention (try/catch tick body, separate OCE
/// handling, DI scope per tick).
///
/// Wakes on whichever comes first: a 10s tick, a telemetry nudge (connectivity/connector changes),
/// a settings-change notification, or an internal wake (on-connect / HA-birth resync). Every wake
/// builds one immutable snapshot and publishes only the topics whose payload changed (retained,
/// QoS 1) — except on a "full sync" (first connect, reconnect, HA birth, or a discovery-relevant
/// settings change), which republishes discovery configs, all states, then "online" last.
/// </summary>
public sealed class MqttPublisherService : BackgroundService, IMqttPublisherStatus
{
    private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(10);

    private static readonly string _swVersion =
        typeof(MqttPublisherService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttSnapshotBuilder _snapshotBuilder;
    private readonly MqttAvailabilityCommandHandler _commandHandler;
    private readonly MqttTelemetryNudge _nudge;
    private readonly IMqttSettingsNotifier _settingsNotifier;
    private readonly ILogger<MqttPublisherService> _logger;

    // Internal wake for on-connect / HA-birth / settings-bridge resyncs. Bounded+drop-write so a
    // burst collapses to one wake.
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    // Last payload published per topic — the diff cache. Touched by the loop and by the command
    // handler's publish closures, so it must be thread-safe.
    private readonly ConcurrentDictionary<string, string> _publishedCache = new();

    private MqttConnection? _connection;
    private MqttTopics? _topics;
    private string? _lastStatusTopic;
    private string? _connSignature;
    private string? _pubSignature;
    private volatile bool _needsFullSync;
    private CancellationToken _stoppingToken;

    // Status snapshot fields (advisory; read by the /api/mqtt/status endpoint on another thread).
    private bool _enabled;
    private string _host = "";
    private int _port;
    private DateTime? _lastConnectedAt;
    private DateTime? _lastPublishAt;
    private string? _lastError;
    private DateTime? _lastErrorAt;

    public MqttPublisherService(
        IServiceScopeFactory scopeFactory,
        MqttSnapshotBuilder snapshotBuilder,
        MqttAvailabilityCommandHandler commandHandler,
        MqttTelemetryNudge nudge,
        IMqttSettingsNotifier settingsNotifier,
        ILogger<MqttPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _snapshotBuilder = snapshotBuilder;
        _commandHandler = commandHandler;
        _nudge = nudge;
        _settingsNotifier = settingsNotifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _logger.LogInformation("MQTT publisher service started.");

        // Bridge the settings notifier (a shared seam, not a channel) into the wake channel so the
        // loop has a single wait point.
        _ = Task.Run(() => BridgeSettingsNotifierAsync(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in MQTT publisher tick.");
                _lastError = ex.Message;
                _lastErrorAt = DateTime.UtcNow;
            }

            await WaitForNextAsync(stoppingToken);
        }

        _logger.LogInformation("MQTT publisher service stopping.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        MqttSettings settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            settings = await db.MqttSettings.AsNoTracking().FirstAsync(ct);
        }

        _enabled = settings.Enabled;
        _host = settings.Host;
        _port = settings.Port;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Host))
        {
            await TearDownConnectionAsync();
            return;
        }

        var connSignature = ConnSignature(settings);
        if (_connection is null || connSignature != _connSignature)
        {
            await RebuildConnectionAsync(settings, connSignature);
        }

        if (_connection is not { } connection)
        {
            return; // rebuild failed; retry next tick (error already recorded)
        }

        var snapshot = await _snapshotBuilder.BuildAsync(ct);

        var pubSignature = PubSignature(snapshot);
        var fullSync = _needsFullSync || pubSignature != _pubSignature;

        if (fullSync)
        {
            await FullPublishAsync(connection, settings, snapshot);
            _pubSignature = pubSignature;
            _needsFullSync = false;
        }
        else
        {
            await DiffPublishAsync(connection, snapshot);
        }

        _lastPublishAt = DateTime.UtcNow;
    }

    /// <summary>Publish everything, ordered: discovery configs → all states → "online" LAST.</summary>
    private async Task FullPublishAsync(MqttConnection connection, MqttSettings settings, MqttSnapshot snapshot)
    {
        var topics = _topics!;

        var configs = HaDiscoveryConfigBuilder.Build(settings, snapshot.Currency, _swVersion, snapshot.ChargePointId);
        foreach (var config in configs)
        {
            await connection.PublishAsync(config.Topic, config.Payload, retain: true);
        }

        foreach (var (topic, payload) in EnumerateStateTopics(topics, snapshot))
        {
            await connection.PublishAsync(topic, payload, retain: true);
            _publishedCache[topic] = payload;
        }

        // Online last, so HA never sees available entities before their states.
        await connection.PublishAsync(topics.Status, "online", retain: true);
        _publishedCache[topics.Status] = "online";
    }

    private async Task DiffPublishAsync(MqttConnection connection, MqttSnapshot snapshot)
    {
        foreach (var (topic, payload) in EnumerateStateTopics(_topics!, snapshot))
        {
            if (!_publishedCache.TryGetValue(topic, out var last) || last != payload)
            {
                await connection.PublishAsync(topic, payload, retain: true);
                _publishedCache[topic] = payload;
            }
        }
    }

    private static IEnumerable<(string Topic, string Payload)> EnumerateStateTopics(MqttTopics t, MqttSnapshot s)
    {
        yield return (t.PowerKw, s.PowerKw);
        yield return (t.CarSoc, s.CarSoc);
        yield return (t.Connected, s.Connected);
        yield return (t.ConnectorStatus, s.ConnectorStatus);
        yield return (t.SessionEnergyKwh, s.SessionEnergyKwh);
        yield return (t.SessionCost, s.SessionCost);
        yield return (t.LastHeartbeat, s.LastHeartbeat);
        yield return (t.PlanDeadline, s.PlanDeadline);
        yield return (t.PlanTargetSoc, s.PlanTargetSoc);
        yield return (t.PlanRequiredKwh, s.PlanRequiredKwh);
        yield return (t.PlanEstimatedCost, s.PlanEstimatedCost);
        yield return (t.SwitchState, s.SwitchState);
        yield return (t.SwitchAvailable, s.SwitchAvailable);
    }

    private async Task RebuildConnectionAsync(MqttSettings settings, string connSignature)
    {
        await TearDownConnectionAsync();

        var topics = new MqttTopics(settings.BaseTopic, settings.DiscoveryPrefix);
        var connection = new MqttConnection(MqttConnectionOptions.From(settings), _logger)
        {
            OnMessage = HandleMessageAsync,
            OnConnected = OnConnectedAsync,
            OnConnectingFailed = OnConnectingFailedAsync,
        };

        try
        {
            await connection.StartAsync();
            _connection = connection;
            _topics = topics;
            _lastStatusTopic = topics.Status;
            _connSignature = connSignature;
            _pubSignature = null;
            _publishedCache.Clear();
            _needsFullSync = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT connection to {Host}:{Port}.", settings.Host, settings.Port);
            _lastError = ex.Message;
            _lastErrorAt = DateTime.UtcNow;
            await connection.DisposeAsync();
        }
    }

    private async Task TearDownConnectionAsync()
    {
        if (_connection is { } connection)
        {
            await PublishOfflineAndFlushAsync(connection);
            await connection.DisposeAsync();
        }

        _connection = null;
        _topics = null;
        _connSignature = null;
        _pubSignature = null;
        _publishedCache.Clear();
    }

    private Task OnConnectedAsync()
    {
        _needsFullSync = true;
        _lastConnectedAt = DateTime.UtcNow;
        _lastError = null;
        _wake.Writer.TryWrite(true);
        return Task.CompletedTask;
    }

    private Task OnConnectingFailedAsync(string reason)
    {
        // Throttle logging to state changes — the managed client retries every 5s.
        if (_lastError != reason)
        {
            _logger.LogWarning("MQTT connection attempt failed: {Reason}", reason);
        }

        _lastError = reason;
        _lastErrorAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(string topic, string payload)
    {
        if (_topics is not { } topics)
        {
            return;
        }

        if (topic == topics.SwitchSet)
        {
            await _commandHandler.HandleAsync(payload, PublishSwitchStateAsync, PublishSwitchAvailableAsync, _stoppingToken);
        }
        else if (topic == topics.HaBirth)
        {
            if (string.Equals(payload.Trim(), "online", StringComparison.OrdinalIgnoreCase))
            {
                _ = DelayedFullSyncAsync();
            }
            else
            {
                _logger.LogDebug("Ignoring HA birth payload '{Payload}'.", payload);
            }
        }
    }

    // Publish closures handed to the command handler: they publish AND update the diff cache, so the
    // loop's next diff stays consistent with what's on the broker.
    private async Task PublishSwitchStateAsync(bool on)
    {
        if (_connection is not { } connection || _topics is not { } topics)
        {
            return;
        }

        var payload = on ? "ON" : "OFF";
        await connection.PublishAsync(topics.SwitchState, payload, retain: true);
        _publishedCache[topics.SwitchState] = payload;
    }

    private async Task PublishSwitchAvailableAsync(bool available)
    {
        if (_connection is not { } connection || _topics is not { } topics)
        {
            return;
        }

        var payload = available ? "online" : "offline";
        await connection.PublishAsync(topics.SwitchAvailable, payload, retain: true);
        _publishedCache[topics.SwitchAvailable] = payload;
    }

    private async Task DelayedFullSyncAsync()
    {
        try
        {
            // HA needs a moment after birth to be ready for discovery configs.
            await Task.Delay(TimeSpan.FromSeconds(2), _stoppingToken);
            _needsFullSync = true;
            _wake.Writer.TryWrite(true);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HA birth resync scheduling failed.");
        }
    }

    private async Task BridgeSettingsNotifierAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _settingsNotifier.WaitForChangeAsync(ct);
                _wake.Writer.TryWrite(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MQTT settings-notifier bridge error.");
            }
        }
    }

    private async Task WaitForNextAsync(CancellationToken ct)
    {
        var delay = Task.Delay(_tickInterval, ct);
        var wake = _wake.Reader.WaitToReadAsync(ct).AsTask();
        var nudge = _nudge.Reader.WaitToReadAsync(ct).AsTask();

        // WhenAny never throws; the first signal wins. Drain the channels so a stale token can't
        // immediately re-fire the next iteration.
        await Task.WhenAny(delay, wake, nudge);

        while (_wake.Reader.TryRead(out _)) { }
        while (_nudge.Reader.TryRead(out _)) { }
    }

    private static string ConnSignature(MqttSettings s) =>
        // BaseTopic + DiscoveryPrefix are included because they change the LWT topic and the
        // subscribe topics, which are fixed at connect time — so a change must reconnect.
        string.Join('|', s.Host, s.Port, s.Username, s.Password, s.UseTls, s.ClientId, s.BaseTopic, s.DiscoveryPrefix);

    private static string PubSignature(MqttSnapshot s) =>
        // Currency + ChargePointId are the only discovery inputs that can change without a reconnect;
        // a change forces a full resync so discovery configs are republished.
        string.Join('|', s.Currency, s.ChargePointId);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop the loop FIRST so an in-flight tick can't re-publish "online" after our "offline".
        await base.StopAsync(cancellationToken);

        // LWT only fires on ungraceful drops, so announce offline explicitly on a clean shutdown.
        if (_connection is { } connection)
        {
            await PublishOfflineAndFlushAsync(connection);
            await connection.DisposeAsync();
            _connection = null;
        }
    }

    /// <summary>
    /// Publish the retained "offline" status and wait for it to actually reach the broker before the
    /// caller disposes the client (a clean disconnect suppresses the LWT and drops the queue).
    /// </summary>
    private async Task PublishOfflineAndFlushAsync(MqttConnection connection)
    {
        if (!connection.IsConnected || _lastStatusTopic is null)
        {
            return;
        }

        try
        {
            await connection.PublishAsync(_lastStatusTopic, "offline", retain: true);
            await connection.FlushAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish/flush offline status.");
        }
    }

    public MqttStatusSnapshot GetSnapshot() => new(
        Enabled: _enabled,
        Connected: _connection?.IsConnected ?? false,
        Host: _host,
        Port: _port,
        LastConnectedAt: _lastConnectedAt,
        LastPublishAt: _lastPublishAt,
        LastError: _lastError,
        LastErrorAt: _lastErrorAt);
}
