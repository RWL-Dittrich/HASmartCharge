using HASmartCharge.Backend.Services;

namespace HASmartCharge.Backend.Tests;

public class PlugStateTrackerTests
{
    [Fact]
    public void UnconfirmedPlugIn_NeverBecomesAtHome()
    {
        var tracker = new PlugStateTracker();

        // Car reports plugged at a public charger: our charger is online and shows no cable.
        tracker.RegisterAndDetectRisingEdge(confirmed: false, detached: true);
        tracker.RegisterAndDetectRisingEdge(confirmed: false, detached: true);

        Assert.False(tracker.IsAtHome);
    }

    [Fact]
    public void ChargerGoingOfflineMidCharge_KeepsLatchAndControl()
    {
        var tracker = new PlugStateTracker();

        tracker.RegisterAndDetectRisingEdge(confirmed: false, detached: true); // baseline: unplugged
        Assert.True(tracker.RegisterAndDetectRisingEdge(confirmed: true, detached: false)); // rising edge

        // Charger drops its WebSocket: neither confirmed nor positively detached.
        Assert.False(tracker.RegisterAndDetectRisingEdge(confirmed: false, detached: false));
        Assert.True(tracker.IsAtHome);

        // Cable positively out at home → latch clears.
        tracker.RegisterAndDetectRisingEdge(confirmed: false, detached: true);
        Assert.False(tracker.IsAtHome);
    }

    [Fact]
    public void FirstObservationWhilePluggedIn_SetsBaselineWithoutRisingEdge()
    {
        var tracker = new PlugStateTracker();

        Assert.False(tracker.RegisterAndDetectRisingEdge(confirmed: true, detached: false));
        Assert.True(tracker.IsAtHome);
    }
}
