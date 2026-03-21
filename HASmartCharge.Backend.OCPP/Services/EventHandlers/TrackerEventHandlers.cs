using HASmartCharge.Application.Events;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Domain.Events;

namespace HASmartCharge.Backend.OCPP.Services.EventHandlers;

public sealed class ChargerConnectedHandler : IDomainEventHandler<ChargerConnected>
{
    private readonly ChargerStatusTracker _tracker;
    public ChargerConnectedHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ChargerConnected evt, CancellationToken ct = default)
    {
        _tracker.OnChargerConnected(evt.ChargePointId);
        return Task.CompletedTask;
    }
}

public sealed class ChargerDisconnectedHandler : IDomainEventHandler<ChargerDisconnected>
{
    private readonly ChargerStatusTracker _tracker;
    public ChargerDisconnectedHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ChargerDisconnected evt, CancellationToken ct = default)
    {
        _tracker.OnChargerDisconnected(evt.ChargePointId);
        return Task.CompletedTask;
    }
}

public sealed class ChargerRegisteredHandler : IDomainEventHandler<ChargerRegistered>
{
    private readonly ChargerStatusTracker _tracker;
    public ChargerRegisteredHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ChargerRegistered evt, CancellationToken ct = default)
    {
        _tracker.OnBootNotification(evt.ChargePointId, new BootNotificationRequest
        {
            ChargePointVendor = evt.Vendor,
            ChargePointModel = evt.Model,
            ChargePointSerialNumber = evt.SerialNumber,
            FirmwareVersion = evt.FirmwareVersion
        });
        return Task.CompletedTask;
    }
}

public sealed class ChargingSessionStartedHandler : IDomainEventHandler<ChargingSessionStarted>
{
    private readonly ChargerStatusTracker _tracker;
    public ChargingSessionStartedHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ChargingSessionStarted evt, CancellationToken ct = default)
    {
        _tracker.OnStartTransaction(evt.ChargePointId, new StartTransactionRequest
        {
            ConnectorId = evt.ConnectorId,
            IdTag = evt.IdTag,
            MeterStart = evt.MeterStartWh,
            Timestamp = evt.OccurredAt.UtcDateTime
        }, evt.TransactionId);
        return Task.CompletedTask;
    }
}

public sealed class ChargingSessionCompletedHandler : IDomainEventHandler<ChargingSessionCompleted>
{
    private readonly ChargerStatusTracker _tracker;
    public ChargingSessionCompletedHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ChargingSessionCompleted evt, CancellationToken ct = default)
    {
        _tracker.OnStopTransaction(evt.ChargePointId, new StopTransactionRequest
        {
            TransactionId = evt.TransactionId,
            MeterStop = evt.MeterStopWh,
            Reason = evt.StopReason,
            Timestamp = evt.OccurredAt.UtcDateTime
        });
        return Task.CompletedTask;
    }
}

public sealed class ConnectorStatusUpdatedHandler : IDomainEventHandler<ConnectorStatusUpdated>
{
    private readonly ChargerStatusTracker _tracker;
    public ConnectorStatusUpdatedHandler(ChargerStatusTracker tracker) => _tracker = tracker;
    public Task HandleAsync(ConnectorStatusUpdated evt, CancellationToken ct = default)
    {
        _tracker.OnStatusNotification(evt.ChargePointId, new StatusNotificationRequest
        {
            ConnectorId = evt.ConnectorId,
            Status = evt.Status,
            ErrorCode = evt.ErrorCode ?? "NoError"
        });
        return Task.CompletedTask;
    }
}
