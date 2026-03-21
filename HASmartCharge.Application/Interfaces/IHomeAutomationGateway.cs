namespace HASmartCharge.Application.Interfaces;

/// <summary>
/// Outbound port for publishing charger and session state
/// to a home automation system, decoupled from any specific platform.
/// </summary>
public interface IHomeAutomationGateway
{
    /// <summary>
    /// Publish charger connection state to the home automation system.
    /// </summary>
    Task PublishChargerStateAsync(string chargerId, bool isConnected, CancellationToken ct = default);

    /// <summary>
    /// Publish connector status to the home automation system.
    /// </summary>
    Task PublishConnectorStatusAsync(string chargerId, int connectorId, string status, CancellationToken ct = default);

    /// <summary>
    /// Publish charging session lifecycle to the home automation system.
    /// </summary>
    Task PublishChargingSessionAsync(string chargerId, int connectorId, int transactionId, bool isActive, CancellationToken ct = default);

    /// <summary>
    /// Check if the home automation system is currently connected.
    /// </summary>
    bool IsConnected();
}
