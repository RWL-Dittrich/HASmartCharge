using System.Threading.Channels;
using HASmartCharge.Backend.DB.Models;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// Lets the settings PUT wake the publisher loop the instant MQTT settings change, instead of
/// waiting for the next 10s tick. Singleton, single consumer (the publisher loop).
///
/// Backed by a bounded (capacity 1, drop-write) channel: a <see cref="NotifyChanged"/> that arrives
/// before the consumer waits stays queued, so <see cref="WaitForChangeAsync"/> returns immediately —
/// no lost wakeups. Multiple notifies coalesce into one. (The publisher also re-diffs settings every
/// tick, so even a dropped signal would self-heal.)
/// </summary>
public interface IMqttSettingsNotifier
{
    void NotifyChanged();
    Task WaitForChangeAsync(CancellationToken ct);
}

public sealed class MqttSettingsNotifier : IMqttSettingsNotifier
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    public void NotifyChanged() => _channel.Writer.TryWrite(true);

    public async Task WaitForChangeAsync(CancellationToken ct)
    {
        await _channel.Reader.WaitToReadAsync(ct);
        while (_channel.Reader.TryRead(out _))
        {
            // Coalesce any additional pending signals into this single wake.
        }
    }
}

/// <summary>
/// In-memory publisher status for <c>GET /api/mqtt/status</c>. Implemented by
/// <c>MqttPublisherService</c>. Timestamps are captured with <c>DateTime.UtcNow</c> in-process,
/// so they already carry <c>Kind=Utc</c> — no <c>EnsureUtc</c> re-stamping is needed.
/// </summary>
public interface IMqttPublisherStatus
{
    MqttStatusSnapshot GetSnapshot();
}

public record MqttStatusSnapshot(
    bool Enabled,
    bool Connected,
    string Host,
    int Port,
    DateTime? LastConnectedAt,
    DateTime? LastPublishAt,
    string? LastError,
    DateTime? LastErrorAt);

/// <summary>One-shot broker connectivity check for <c>POST /api/mqtt/test</c>.</summary>
public interface IMqttConnectionTester
{
    Task<MqttTestResult> TestAsync(MqttSettings settings, CancellationToken ct = default);
}

public record MqttTestResult(bool Success, string? Error);

public sealed class MqttConnectionTester : IMqttConnectionTester
{
    public async Task<MqttTestResult> TestAsync(MqttSettings settings, CancellationToken ct = default)
    {
        var (ok, error) = await MqttConnection.TestConnectionAsync(MqttConnectionOptions.From(settings), ct);
        return new MqttTestResult(ok, error);
    }
}
