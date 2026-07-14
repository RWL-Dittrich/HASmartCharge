namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// The single, shared Operative/Inoperative switch rule. Both the state publisher and the
/// command validator use it so they can never diverge. Mirrors the dashboard rule
/// (DashboardPage.tsx): the connector may be toggled only from a settled Available/Unavailable
/// state, and only when the charger is connected.
/// </summary>
public static class MqttSwitchRule
{
    private const string Available = "Available";
    private const string Unavailable = "Unavailable";

    /// <summary>Switch is ON (Operative) unless the connector is explicitly Unavailable.</summary>
    public static bool IsOn(string? connectorStatus) =>
        !string.Equals(connectorStatus, Unavailable, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Switch is togglable (HA shows it enabled) only when connected AND the connector sits in a
    /// settled Available/Unavailable state — every transitional/charging/faulted/disconnected
    /// case greys it out.
    /// </summary>
    public static bool IsAvailable(bool isConnected, string? connectorStatus) =>
        isConnected
        && (string.Equals(connectorStatus, Available, StringComparison.OrdinalIgnoreCase)
            || string.Equals(connectorStatus, Unavailable, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a command to turn the switch <paramref name="desiredOn"/> is both allowed and not a
    /// no-op: make-Operative only from Unavailable, make-Inoperative only from Available, and only
    /// when connected. Server-side re-validation — the retained availability topic is advisory and a
    /// raw <c>mosquitto_pub</c> must not bypass this.
    /// </summary>
    public static bool CanApply(bool isConnected, string? connectorStatus, bool desiredOn)
    {
        if (!isConnected)
        {
            return false;
        }

        return desiredOn
            ? string.Equals(connectorStatus, Unavailable, StringComparison.OrdinalIgnoreCase)
            : string.Equals(connectorStatus, Available, StringComparison.OrdinalIgnoreCase);
    }
}
