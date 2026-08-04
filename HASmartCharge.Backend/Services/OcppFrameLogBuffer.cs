using HASmartCharge.Backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HASmartCharge.Backend.Services;

/// <summary>One raw OCPP frame, tagged with direction and the charge point it belongs to.</summary>
public record OcppFrameRecord(DateTime TimestampUtc, string ChargePointId, string Direction, string Frame);

/// <summary>
/// In-memory ring buffer of the last 50 OCPP frames (both directions), broadcast live to the
/// developer-tab log over SignalR (<see cref="OcppLogHub"/>) and replayed to newly-connected
/// clients via <see cref="Snapshot"/>. Fed by <c>OcppRawLog.FrameObserved</c>, which fires
/// synchronously on the OCPP send/receive path, so <see cref="Publish"/> must never throw or block.
/// </summary>
public class OcppFrameLogBuffer
{
    private const int MaxFrames = 50;

    private readonly IHubContext<OcppLogHub> _hub;
    private readonly ILogger<OcppFrameLogBuffer> _logger;
    private readonly object _gate = new();
    private readonly Queue<OcppFrameRecord> _frames = new();

    public OcppFrameLogBuffer(IHubContext<OcppLogHub> hub, ILogger<OcppFrameLogBuffer> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <summary>Record a frame and fire-and-forget broadcast it. Never throws.</summary>
    public void Publish(string chargePointId, string direction, string frame)
    {
        try
        {
            var record = new OcppFrameRecord(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc), chargePointId, direction, frame);

            lock (_gate)
            {
                _frames.Enqueue(record);
                while (_frames.Count > MaxFrames)
                {
                    _frames.Dequeue();
                }
            }

            _ = _hub.Clients.All.SendAsync("frame", record).ContinueWith(t =>
            {
                if (t.Exception is not null)
                {
                    _logger.LogWarning(t.Exception, "Failed to broadcast OCPP frame to developer-tab log.");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            // Diagnostics must never disrupt OCPP message processing.
            _logger.LogWarning(ex, "Failed to publish OCPP frame to developer-tab log.");
        }
    }

    /// <summary>Copy of the buffered frames, oldest first.</summary>
    public IReadOnlyList<OcppFrameRecord> Snapshot()
    {
        lock (_gate)
        {
            return _frames.ToArray();
        }
    }
}
