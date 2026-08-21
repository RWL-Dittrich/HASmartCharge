using System.Threading.Channels;
using HASmartCharge.Backend.Services.Telemetry;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// A telemetry sink whose only job is to wake the publisher loop sub-second when a connectivity or
/// connector-status event happens, so connected/connector/switch state react without waiting for
/// the 10s tick. A bounded, drop-write channel of capacity 1 coalesces bursts into a single wake.
/// Writes never throw and never publish anything themselves (exactly one publish path — the loop).
/// </summary>
public sealed class MqttTelemetryNudge : IChargerTelemetry
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<bool> Reader => _channel.Reader;

    private void Nudge() => _channel.Writer.TryWrite(true);

    public void OnConnected(string chargerId) => Nudge();
    public void OnDisconnected(string chargerId) => Nudge();
    public void OnConnectorStatus(string chargerId, int connectorId, ConnectorState state, string? errorCode) => Nudge();

    public void OnChargerInfo(string chargerId, ChargerDeviceInfo info) { }
    // Heartbeat updates only a timestamp; the 10s publisher tick carries it. No sub-second nudge needed.
    public void OnHeartbeat(string chargerId) { }
    public void OnSessionStarted(string chargerId, int connectorId, int sessionId, double meterStartKwh, string? tag, DateTimeOffset startedAt) { }
    public void OnSessionStopped(string chargerId, int sessionId, double meterStopKwh, string? reason, DateTimeOffset stoppedAt) { }
    public void OnMeterSample(string chargerId, int connectorId, ChargerMeterSample sample) { }
}
