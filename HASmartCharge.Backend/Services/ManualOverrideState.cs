namespace HASmartCharge.Backend.Services;

/// <summary>
/// Singleton, thread-safe holder for a temporary manual-override window. While active,
/// <see cref="ChargeOrchestratorService"/> skips automatic start/stop toggling for the
/// active plan, leaving whatever the manual call did in place.
/// </summary>
public class ManualOverrideState
{
    private readonly object _gate = new();
    private DateTime? _overrideUntilUtc;

    public DateTime? OverrideUntilUtc
    {
        get { lock (_gate) { return _overrideUntilUtc; } }
    }

    public bool IsActive
    {
        get { lock (_gate) { return _overrideUntilUtc is { } until && DateTime.UtcNow < until; } }
    }

    public void Activate(TimeSpan duration)
    {
        lock (_gate)
        {
            _overrideUntilUtc = DateTime.UtcNow.Add(duration);
        }
    }
}
