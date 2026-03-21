using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.HomeAssistant.Services;

/// <summary>
/// Home Assistant implementation of the home automation gateway.
/// Publishes charger/session state to Home Assistant via its API.
/// </summary>
public class HomeAssistantGateway : IHomeAutomationGateway
{
    private readonly IHomeAssistantConnectionManager _connectionManager;
    private readonly ILogger<HomeAssistantGateway> _logger;

    public HomeAssistantGateway(
        IHomeAssistantConnectionManager connectionManager,
        ILogger<HomeAssistantGateway> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public Task PublishChargerStateAsync(string chargerId, bool isConnected, CancellationToken ct = default)
    {
        if (!IsConnected())
        {
            _logger.LogDebug("Skipping charger state publish — Home Assistant not connected");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Publishing charger {ChargerId} state: connected={IsConnected}", chargerId, isConnected);
        // TODO: Implement HA API call to publish charger state (e.g. POST /api/states/sensor.charger_{id}_status)
        return Task.CompletedTask;
    }

    public Task PublishConnectorStatusAsync(string chargerId, int connectorId, string status, CancellationToken ct = default)
    {
        if (!IsConnected())
        {
            _logger.LogDebug("Skipping connector status publish — Home Assistant not connected");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Publishing connector {ChargerId}/{ConnectorId} status: {Status}", chargerId, connectorId, status);
        // TODO: Implement HA API call to publish connector status
        return Task.CompletedTask;
    }

    public Task PublishChargingSessionAsync(string chargerId, int connectorId, int transactionId, bool isActive, CancellationToken ct = default)
    {
        if (!IsConnected())
        {
            _logger.LogDebug("Skipping charging session publish — Home Assistant not connected");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Publishing charging session {TransactionId} on {ChargerId}/{ConnectorId}: active={IsActive}",
            transactionId, chargerId, connectorId, isActive);
        // TODO: Implement HA API call to publish charging session state
        return Task.CompletedTask;
    }

    public bool IsConnected()
    {
        return _connectionManager.IsConnected();
    }
}
