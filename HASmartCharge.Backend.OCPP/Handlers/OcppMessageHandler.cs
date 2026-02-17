using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Handlers;

/// <summary>
/// Delegate for handling OCPP requests and generating responses
/// </summary>
public delegate Task<object> OcppRequestHandler(JsonElement payload);

/// <summary>
/// Central message handler for OCPP 1.6J protocol
/// Routes incoming messages to appropriate handlers
/// </summary>
public class OcppMessageHandler
{
    private readonly Dictionary<string, OcppRequestHandler> _handlers = new();
    private readonly ILogger<OcppMessageHandler> _logger;
    private readonly string _chargePointId;

    public OcppMessageHandler(string chargePointId, ILogger<OcppMessageHandler> logger)
    {
        _chargePointId = chargePointId;
        _logger = logger;
    }

    /// <summary>
    /// Register a handler for a specific OCPP action
    /// </summary>
    public void RegisterHandler(string action, OcppRequestHandler handler)
    {
        _handlers[action] = handler;
    }

    /// <summary>
    /// Process an incoming OCPP message and generate a response
    /// </summary>
    public async Task<string> HandleMessage(string jsonMessage)
    {
        try
        {
            _logger.LogDebug("[{ChargePointId}] Received: {Message}", _chargePointId, jsonMessage);

            OcppMessage message = OcppMessage.Parse(jsonMessage);

            if (message.MessageType == (int)OcppMessageType.Call)
            {
                return await HandleCall(message);
            }
            else if (message.MessageType == (int)OcppMessageType.CallResult)
            {
                _logger.LogDebug("[{ChargePointId}] Received CallResult for message {MessageId}", 
                    _chargePointId, message.MessageId);
                return string.Empty; // No response needed for CallResult
            }
            else if (message.MessageType == (int)OcppMessageType.CallError)
            {
                _logger.LogWarning("[{ChargePointId}] Received CallError: {ErrorCode}", 
                    _chargePointId, message.Action);
                return string.Empty; // No response needed for CallError
            }

            throw new InvalidOperationException($"Unknown message type: {message.MessageType}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error handling message", _chargePointId);
            
            OcppErrorMessage errorMessage = new OcppErrorMessage
            {
                MessageId = "error",
                ErrorCode = "InternalError",
                ErrorDescription = ex.Message,
                ErrorDetails = new { }
            };
            
            return errorMessage.ToJson();
        }
    }

    private async Task<string> HandleCall(OcppMessage message)
    {
        string action = message.Action ?? string.Empty;

        if (!_handlers.ContainsKey(action))
        {
            _logger.LogWarning("[{ChargePointId}] No handler for action: {Action}", 
                _chargePointId, action);
            
            OcppErrorMessage errorMessage = new OcppErrorMessage
            {
                MessageId = message.MessageId,
                ErrorCode = "NotImplemented",
                ErrorDescription = $"Action '{action}' is not implemented",
                ErrorDetails = new { }
            };
            
            return errorMessage.ToJson();
        }

        try
        {
            OcppRequestHandler handler = _handlers[action];
            object response = await handler(message.Payload);

            OcppMessage responseMessage = new OcppMessage
            {
                MessageType = (int)OcppMessageType.CallResult,
                MessageId = message.MessageId,
                Payload = JsonSerializer.SerializeToElement(response)
            };

            string responseJson = responseMessage.ToJson();
            _logger.LogDebug("[{ChargePointId}] Sending: {Response}", _chargePointId, responseJson);
            
            return responseJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error executing handler for {Action}", 
                _chargePointId, action);
            
            OcppErrorMessage errorMessage = new OcppErrorMessage
            {
                MessageId = message.MessageId,
                ErrorCode = "InternalError",
                ErrorDescription = ex.Message,
                ErrorDetails = new { }
            };
            
            return errorMessage.ToJson();
        }
    }
}
