namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// MQTT broker connection + Home Assistant discovery settings, used to publish charging
/// telemetry into HA as a discovered device. Single row (Id = 1).
///
/// <para>
/// Disabled by default so there is zero behavior change until the user opts in.
/// </para>
/// <para>
/// <see cref="Password"/> is stored in plaintext, matching the existing precedent for
/// <c>HomeAssistantConnection</c> OAuth tokens. DPAPI is deliberately NOT used: it is
/// Windows-only and would break the Linux Home Assistant add-on container.
/// </para>
/// </summary>
public class MqttSettings
{
    public int Id { get; set; }

    /// <summary>Master switch. When false the publisher stays idle and publishes nothing.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Broker host. "core-mosquitto" is the HA Mosquitto add-on host; dev uses "localhost".</summary>
    public string Host { get; set; } = "core-mosquitto";

    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    /// <summary>Plaintext broker password — see the type remarks for why it is not encrypted.</summary>
    public string? Password { get; set; }

    public bool UseTls { get; set; } = false;

    public string ClientId { get; set; } = "hasmartcharge";

    /// <summary>Root of the state/command topic tree, e.g. "hasmartcharge/charger/power_kw".</summary>
    public string BaseTopic { get; set; } = "hasmartcharge";

    /// <summary>HA MQTT discovery prefix; retained config topics live under it.</summary>
    public string DiscoveryPrefix { get; set; } = "homeassistant";
}
