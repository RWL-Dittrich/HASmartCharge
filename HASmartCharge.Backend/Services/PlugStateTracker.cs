namespace HASmartCharge.Backend.Services;

/// <summary>
/// Singleton, thread-safe holder of the last-observed "car plugged in" state, used by
/// <see cref="ChargeOrchestratorService"/> to detect the plug-in rising edge that auto-arms a
/// charge plan. A rising edge fires only when a previously-observed unplugged state becomes
/// plugged — the very first observation just sets the baseline, so a backend restart while the
/// car is plugged in does not spuriously create a plan.
/// </summary>
public class PlugStateTracker
{
    private readonly object _gate = new();
    private bool? _lastPluggedIn;

    /// <summary>Records the current state and returns true only on an unplugged → plugged transition.</summary>
    public bool RegisterAndDetectRisingEdge(bool pluggedNow)
    {
        lock (_gate)
        {
            var rising = pluggedNow && _lastPluggedIn == false;
            _lastPluggedIn = pluggedNow;
            return rising;
        }
    }
}
