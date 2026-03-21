using HASmartCharge.Application.Events;
using HASmartCharge.Application.Interfaces;
using HASmartCharge.Domain.Events;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.HomeAssistant.EventHandlers;

/// <summary>
/// Forwards charger and session domain events to the home automation gateway
/// so that Home Assistant stays in sync with charging state.
/// </summary>
public class HomeAutomationEventHandler :
    IDomainEventHandler<ChargerConnected>,
    IDomainEventHandler<ChargerDisconnected>,
    IDomainEventHandler<ConnectorStatusUpdated>,
    IDomainEventHandler<ChargingSessionStarted>,
    IDomainEventHandler<ChargingSessionCompleted>
{
    private readonly IHomeAutomationGateway _gateway;
    private readonly ILogger<HomeAutomationEventHandler> _logger;

    public HomeAutomationEventHandler(
        IHomeAutomationGateway gateway,
        ILogger<HomeAutomationEventHandler> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task HandleAsync(ChargerConnected domainEvent, CancellationToken ct = default)
    {
        await _gateway.PublishChargerStateAsync(domainEvent.ChargePointId, isConnected: true, ct);
    }

    public async Task HandleAsync(ChargerDisconnected domainEvent, CancellationToken ct = default)
    {
        await _gateway.PublishChargerStateAsync(domainEvent.ChargePointId, isConnected: false, ct);
    }

    public async Task HandleAsync(ConnectorStatusUpdated domainEvent, CancellationToken ct = default)
    {
        await _gateway.PublishConnectorStatusAsync(domainEvent.ChargePointId, domainEvent.ConnectorId, domainEvent.Status, ct);
    }

    public async Task HandleAsync(ChargingSessionStarted domainEvent, CancellationToken ct = default)
    {
        await _gateway.PublishChargingSessionAsync(domainEvent.ChargePointId, domainEvent.ConnectorId, domainEvent.TransactionId, isActive: true, ct);
    }

    public async Task HandleAsync(ChargingSessionCompleted domainEvent, CancellationToken ct = default)
    {
        await _gateway.PublishChargingSessionAsync(domainEvent.ChargePointId, domainEvent.ConnectorId, domainEvent.TransactionId, isActive: false, ct);
    }
}
