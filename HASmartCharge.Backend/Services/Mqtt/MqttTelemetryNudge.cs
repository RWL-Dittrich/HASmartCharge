using System.Threading.Channels;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// A telemetry sink whose only job is to wake the publisher loop sub-second when a connectivity or
/// connector-status event happens, so connected/connector/switch state react without waiting for
/// the 10s tick. A bounded, drop-write channel of capacity 1 coalesces bursts into a single wake.
/// Writes never throw and never publish anything themselves (exactly one publish path — the loop).
/// </summary>
public sealed class MqttTelemetryNudge : IChargerTelemetrySink
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<bool> Reader => _channel.Reader;

    private void Nudge() => _channel.Writer.TryWrite(true);

    public void OnConnected(string chargePointId) => Nudge();
    public void OnDisconnected(string chargePointId) => Nudge();
    public void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode) => Nudge();

    public void OnBoot(string chargePointId, ChargerInfo info) { }
    public void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt) { }
    public void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt) { }
    public void OnMeterValues(string chargePointId, MeterValuesRequest values) { }
}
