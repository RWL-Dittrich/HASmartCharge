using System.Text.Json;
using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Transport;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Application;

/// <summary>
/// Routes OCPP messages to the appropriate ChargePointSession
/// Handles CALL, CALLRESULT, and CALLERROR message types
/// </summary>
public class OcppMessageRouter : IOcppMessageRouter
{
    private readonly ILogger<OcppMessageRouter> _logger;
    private readonly ISessionManager _sessionManager;

    public OcppMessageRouter(
        ILogger<OcppMessageRouter> logger,
        ISessionManager sessionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public async Task<string?> RouteAsync(
        IConnection connection,
        string rawMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("[{ConnectionId}] Routing message: {Message}",
                connection.ConnectionId, rawMessage);

            // Parse the OCPP message
            OcppMessage message = OcppMessage.Parse(rawMessage);

            // Get the session for this connection
            IChargePointSession? session = _sessionManager.GetByConnectionId(connection.ConnectionId);
            if (session == null)
            {
                _logger.LogWarning("[{ConnectionId}] No session found for connection",
                    connection.ConnectionId);
                return CreateErrorResponse("error", "InternalError", "No session found");
            }

            // Route based on message type
            if (message.MessageType == (int)OcppMessageType.Call)
            {
                return await HandleCall(session, message, cancellationToken);
            }
            else if (message.MessageType == (int)OcppMessageType.CallResult)
            {
                await HandleCallResult(session, message, cancellationToken);
                return null; // No response needed for CallResult
            }
            else if (message.MessageType == (int)OcppMessageType.CallError)
            {
                await HandleCallError(session, message, cancellationToken);
                return null; // No response needed for CallError
            }

            _logger.LogWarning("[{ConnectionId}] Unknown message type: {MessageType}",
                connection.ConnectionId, message.MessageType);
            return CreateErrorResponse(message.MessageId, "NotSupported", "Unknown message type");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ConnectionId}] Error routing message",
                connection.ConnectionId);
            return CreateErrorResponse("error", "InternalError", ex.Message);
        }
    }

    private async Task<string> HandleCall(
        IChargePointSession session,
        OcppMessage message,
        CancellationToken cancellationToken)
    {
        string action = message.Action ?? string.Empty;

        _logger.LogDebug("[{ChargePointId}] Handling CALL: {Action}",
            session.ChargePointId, action);

        try
        {
            // Route to the session's handler
            object response = await session.HandleCallAsync(action, message.Payload, cancellationToken);

            // Create CallResult response
            OcppMessage responseMessage = new OcppMessage
            {
                MessageType = (int)OcppMessageType.CallResult,
                MessageId = message.MessageId,
                Payload = JsonSerializer.SerializeToElement(response)
            };

            return responseMessage.ToJson();
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("[{ChargePointId}] Action not supported: {Action}",
                session.ChargePointId, action);
            return CreateErrorResponse(message.MessageId, "NotImplemented",
                $"Action '{action}' is not implemented");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error handling {Action}",
                session.ChargePointId, action);
            return CreateErrorResponse(message.MessageId, "InternalError", ex.Message);
        }
    }

    private Task HandleCallResult(
        IChargePointSession session,
        OcppMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("[{ChargePointId}] Received CallResult for message {MessageId}",
            session.ChargePointId, message.MessageId);

        // Notify session about the result (for correlation tracking if needed)
        return session.HandleCallResultAsync(message.MessageId, message.Payload, cancellationToken);
    }

    private Task HandleCallError(
        IChargePointSession session,
        OcppMessage message,
        CancellationToken cancellationToken)
    {
        string errorCode = message.Action ?? "UnknownError";
        
        _logger.LogWarning("[{ChargePointId}] Received CallError for message {MessageId}: {ErrorCode}",
            session.ChargePointId, message.MessageId, errorCode);

        // Notify session about the error (for correlation tracking if needed)
        return session.HandleCallErrorAsync(message.MessageId, errorCode, message.Payload, cancellationToken);
    }

    private string CreateErrorResponse(string messageId, string errorCode, string errorDescription)
    {
        OcppErrorMessage errorMessage = new OcppErrorMessage
        {
            MessageId = messageId,
            ErrorCode = errorCode,
            ErrorDescription = errorDescription,
            ErrorDetails = new { }
        };

        return errorMessage.ToJson();
    }
}
