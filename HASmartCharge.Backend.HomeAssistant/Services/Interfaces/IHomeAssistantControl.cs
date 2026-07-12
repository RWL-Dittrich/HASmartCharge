namespace HASmartCharge.Backend.HomeAssistant.Services.Interfaces;

public interface IHomeAssistantControl
{
    /// <summary>
    /// Gets the state of a battery SoC sensor entity as a double.
    /// Returns null if not connected, the entity is unavailable/unknown, or the state cannot be parsed.
    /// </summary>
    Task<double?> GetBatterySocAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    /// Gets the raw state string of an entity. Returns null if not connected or the entity is not found.
    /// </summary>
    Task<string?> GetStateAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    /// Calls a Home Assistant service (e.g. domain "switch", service "turn_on") with the given JSON body.
    /// Throws InvalidOperationException if not connected to Home Assistant.
    /// </summary>
    Task CallServiceAsync(string domain, string service, string? dataJson, CancellationToken ct = default);

    /// <summary>
    /// Gets all entities known to Home Assistant, for use in the settings entity picker.
    /// Returns an empty list if not connected.
    /// </summary>
    Task<IReadOnlyList<HaEntitySummary>> GetEntitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all service domains and their service names known to Home Assistant, for use in the
    /// settings service autofill. Returns an empty list if not connected.
    /// </summary>
    Task<IReadOnlyList<HaServiceDomain>> GetServicesAsync(CancellationToken ct = default);
}

public record HaEntitySummary(string EntityId, string? FriendlyName, string? State);

public record HaServiceDomain(string Domain, IReadOnlyList<string> Services);
