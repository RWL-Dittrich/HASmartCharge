namespace HASmartCharge.Backend.Services;

/// <summary>
/// Singleton, thread-safe latch for "the car is plugged into <em>our</em> charger", used by
/// <see cref="ChargeOrchestratorService"/> to decide whether it may control this charge and to
/// detect the plug-in rising edge that auto-arms a charge plan.
/// <para>
/// The latch exists because the two signals (home charger connector status, car-side HA plugged
/// sensor) are not both trustworthy at all times: once the charger drops its WebSocket we can no
/// longer see the connector, but the cable is still in it. So the AND of both signals only has to
/// hold once — from that moment the charge counts as at-home until we <em>positively</em> observe
/// the car detached. Ambiguity (charger offline) holds the last verdict rather than dropping it.
/// A latch that has never been confirmed stays false, and every tick re-tries the confirmation.
/// </para>
/// A rising edge fires only when a previously-observed not-at-home state becomes at-home — the
/// very first observation just sets the baseline, so a backend restart while the car is plugged in
/// does not spuriously create a plan.
/// </summary>
public class PlugStateTracker
{
    private readonly object _gate = new();
    private bool? _atHome;

    /// <summary>Last resolved verdict; false until the first confirmation.</summary>
    public bool IsAtHome
    {
        get { lock (_gate) { return _atHome == true; } }
    }

    /// <summary>
    /// Folds this tick's observation into the latch and returns true only on a
    /// not-at-home → at-home transition.
    /// </summary>
    /// <param name="confirmed">Both signals agree the car is on our charger right now.</param>
    /// <param name="detached">We positively saw the car is not on our charger (not merely unknown).</param>
    public bool RegisterAndDetectRisingEdge(bool confirmed, bool detached)
    {
        lock (_gate)
        {
            if (!detached && !confirmed)
            {
                return false; // ambiguous — hold the previous verdict, no edge.
            }

            var next = confirmed;
            var rising = next && _atHome == false;
            _atHome = next;
            return rising;
        }
    }
}
