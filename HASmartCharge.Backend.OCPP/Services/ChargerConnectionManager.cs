using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Manages active WebSocket connections to charge points
/// </summary>
public class ChargerConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sendLocks = new();
    private readonly ILogger<ChargerConnectionManager> _logger;
    private readonly WebSocketMessageService _messageService;
    private int _messageIdCounter;

    public ChargerConnectionManager(ILogger<ChargerConnectionManager> logger, WebSocketMessageService messageService)
    {
        _logger = logger;
        _messageService = messageService;
    }

    /// <summary>
    /// Register a new charge point connection
    /// </summary>
    public void RegisterConnection(string chargePointId, WebSocket webSocket)
    {
        _connections[chargePointId] = webSocket;
        _sendLocks[chargePointId] = new SemaphoreSlim(1, 1);
        _logger.LogInformation("Registered connection for charge point: {ChargePointId}", chargePointId);
    }

    /// <summary>
    /// Unregister a charge point connection
    /// </summary>
    public void UnregisterConnection(string chargePointId)
    {
        _connections.TryRemove(chargePointId, out _);
        
        if (_sendLocks.TryRemove(chargePointId, out SemaphoreSlim? semaphore))
        {
            semaphore.Dispose();
        }
        
        _logger.LogInformation("Unregistered connection for charge point: {ChargePointId}", chargePointId);
    }

    /// <summary>
    /// Check if a charge point is currently connected
    /// </summary>
    public bool IsConnected(string chargePointId)
    {
        return _connections.TryGetValue(chargePointId, out WebSocket? ws) && 
               ws.State == WebSocketState.Open;
    }

    /// <summary>
    /// Get all currently connected charge point IDs
    /// </summary>
    public IEnumerable<string> GetConnectedChargers()
    {
        return _connections
            .Where(kvp => kvp.Value.State == WebSocketState.Open)
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Send an OCPP command to a charge point
    /// </summary>
    public async Task<bool> SendCommandAsync<TRequest>(string chargePointId, string action, TRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(chargePointId, out WebSocket? webSocket))
        {
            _logger.LogWarning("Charge point not connected: {ChargePointId}", chargePointId);
            return false;
        }

        if (webSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("WebSocket not in open state for charge point: {ChargePointId}", chargePointId);
            return false;
        }

        if (!_sendLocks.TryGetValue(chargePointId, out SemaphoreSlim? sendLock))
        {
            _logger.LogWarning("Send lock not found for charge point: {ChargePointId}", chargePointId);
            return false;
        }

        await sendLock.WaitAsync(cancellationToken);
        try
        {
            string messageId = Interlocked.Increment(ref _messageIdCounter).ToString();
            
            OcppMessage message = new OcppMessage
            {
                MessageType = (int)OcppMessageType.Call,
                MessageId = messageId,
                Action = action,
                Payload = JsonSerializer.SerializeToElement(request)
            };

            string messageJson = message.ToJson();
            
            _logger.LogInformation("Sending {Action} command to {ChargePointId}: {Message}", 
                action, chargePointId, messageJson);

            await _messageService.SendMessageAsync(webSocket, messageJson, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to charge point: {ChargePointId}", chargePointId);
            return false;
        }
        finally
        {
            sendLock.Release();
        }
    }

    /// <summary>
    /// Send a ChangeAvailability command to a charge point
    /// </summary>
    public async Task<bool> ChangeAvailabilityAsync(string chargePointId, int connectorId, string type, CancellationToken cancellationToken = default)
    {
        ChangeAvailabilityRequest request = new ChangeAvailabilityRequest
        {
            ConnectorId = connectorId,
            Type = type
        };
        
        return await SendCommandAsync(chargePointId, "ChangeAvailability", request, cancellationToken);
    }

    /// <summary>
    /// Send a TriggerMessage command to request a BootNotification from a charge point
    /// </summary>
    public async Task<bool> TriggerBootNotificationAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        TriggerMessageRequest request = new TriggerMessageRequest
        {
            RequestedMessage = "BootNotification"
        };
        
        return await SendCommandAsync(chargePointId, "TriggerMessage", request, cancellationToken);
    }
}



