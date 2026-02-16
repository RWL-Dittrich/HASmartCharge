using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using HASmartCharge.Backend.OCPP.Handlers;
using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// OCPP 1.6J Server service that handles OCPP protocol logic
/// </summary>
public class OcppServerService
{
    private readonly ILogger<OcppServerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WebSocketMessageService _webSocketMessageService;
    private readonly ConcurrentDictionary<string, int> _transactionCounters = new();

    public OcppServerService(ILogger<OcppServerService> logger, IServiceProvider serviceProvider, WebSocketMessageService webSocketMessageService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _webSocketMessageService = webSocketMessageService;
    }

    /// <summary>
    /// Handle a WebSocket connection for a charge point
    /// </summary>
    public async Task HandleConnection(WebSocket webSocket, string chargePointId)
    {
        _logger.LogInformation("[{ChargePointId}] Charge point connected", chargePointId);

        OcppMessageHandler messageHandler = CreateMessageHandler(chargePointId);
        
        try
        {
            await ProcessMessages(webSocket, chargePointId, messageHandler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error in connection", chargePointId);
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                    "Server closing connection", CancellationToken.None);
            }
            
            _logger.LogInformation("[{ChargePointId}] Charge point disconnected", chargePointId);
        }
    }

    private async Task ProcessMessages(WebSocket webSocket, string chargePointId, 
        OcppMessageHandler messageHandler)
    {
        while (webSocket.State == WebSocketState.Open)
        {
            string? messageText = await _webSocketMessageService.ReceiveMessageAsync(webSocket);

            if (messageText == null)
            {
                // Connection closed
                break;
            }

            string response = await messageHandler.HandleMessage(messageText);

            if (!string.IsNullOrEmpty(response))
            {
                await _webSocketMessageService.SendMessageAsync(webSocket, response);
            }
        }
    }

    private OcppMessageHandler CreateMessageHandler(string chargePointId)
    {
        ILogger<OcppMessageHandler> logger = _serviceProvider.GetRequiredService<ILogger<OcppMessageHandler>>();
        OcppMessageHandler handler = new OcppMessageHandler(chargePointId, logger);

        // Register all OCPP 1.6J message handlers
        handler.RegisterHandler("BootNotification", async payload => 
            await HandleBootNotification(chargePointId, payload));
        
        handler.RegisterHandler("Heartbeat", async payload => 
            await HandleHeartbeat(chargePointId, payload));
        
        handler.RegisterHandler("Authorize", async payload => 
            await HandleAuthorize(chargePointId, payload));
        
        handler.RegisterHandler("StartTransaction", async payload => 
            await HandleStartTransaction(chargePointId, payload));
        
        handler.RegisterHandler("StopTransaction", async payload => 
            await HandleStopTransaction(chargePointId, payload));
        
        handler.RegisterHandler("MeterValues", async payload => 
            await HandleMeterValues(chargePointId, payload));
        
        handler.RegisterHandler("StatusNotification", async payload => 
            await HandleStatusNotification(chargePointId, payload));
        
        handler.RegisterHandler("DataTransfer", async payload => 
            await HandleDataTransfer(chargePointId, payload));
        
        handler.RegisterHandler("DiagnosticsStatusNotification", async payload => 
            await HandleDiagnosticsStatusNotification(chargePointId, payload));
        
        handler.RegisterHandler("FirmwareStatusNotification", async payload => 
            await HandleFirmwareStatusNotification(chargePointId, payload));

        return handler;
    }

    // ==================== Handler Methods ====================

    private Task<object> HandleBootNotification(string chargePointId, JsonElement payload)
    {
        BootNotificationRequest? request = JsonSerializer.Deserialize<BootNotificationRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] BootNotification: Vendor={Vendor}, Model={Model}, Serial={Serial}",
            chargePointId, 
            request?.ChargePointVendor, 
            request?.ChargePointModel,
            request?.ChargePointSerialNumber);

        BootNotificationResponse response = new BootNotificationResponse
        {
            Status = "Accepted",
            CurrentTime = DateTime.UtcNow,
            Interval = 60
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleHeartbeat(string chargePointId, JsonElement payload)
    {
        _logger.LogDebug("[{ChargePointId}] Heartbeat received", chargePointId);

        HeartbeatResponse response = new HeartbeatResponse
        {
            CurrentTime = DateTime.UtcNow
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleAuthorize(string chargePointId, JsonElement payload)
    {
        AuthorizeRequest? request = JsonSerializer.Deserialize<AuthorizeRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] Authorize: IdTag={IdTag}",
            chargePointId, request?.IdTag);

        AuthorizeResponse response = new AuthorizeResponse
        {
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted",
                ExpiryDate = DateTime.UtcNow.AddHours(24)
            }
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleStartTransaction(string chargePointId, JsonElement payload)
    {
        StartTransactionRequest? request = JsonSerializer.Deserialize<StartTransactionRequest>(payload.GetRawText());
        
        int transactionId = _transactionCounters.AddOrUpdate(chargePointId, 1, (key, value) => value + 1);
        
        _logger.LogInformation("[{ChargePointId}] StartTransaction: Connector={Connector}, IdTag={IdTag}, MeterStart={MeterStart}, TransactionId={TransactionId}",
            chargePointId, 
            request?.ConnectorId, 
            request?.IdTag,
            request?.MeterStart,
            transactionId);

        StartTransactionResponse response = new StartTransactionResponse
        {
            TransactionId = transactionId,
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted",
                ExpiryDate = DateTime.UtcNow.AddHours(24)
            }
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleStopTransaction(string chargePointId, JsonElement payload)
    {
        StopTransactionRequest? request = JsonSerializer.Deserialize<StopTransactionRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] StopTransaction: TransactionId={TransactionId}, IdTag={IdTag}, MeterStop={MeterStop}, Reason={Reason}",
            chargePointId, 
            request?.TransactionId, 
            request?.IdTag,
            request?.MeterStop,
            request?.Reason);

        StopTransactionResponse response = new StopTransactionResponse
        {
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted"
            }
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleMeterValues(string chargePointId, JsonElement payload)
    {
        MeterValuesRequest? request = JsonSerializer.Deserialize<MeterValuesRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] MeterValues: Connector={Connector}, TransactionId={TransactionId}, Values={ValueCount}",
            chargePointId, 
            request?.ConnectorId, 
            request?.TransactionId,
            request?.MeterValue?.Count ?? 0);

        MeterValuesResponse response = new MeterValuesResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleStatusNotification(string chargePointId, JsonElement payload)
    {
        StatusNotificationRequest? request = JsonSerializer.Deserialize<StatusNotificationRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] StatusNotification: Connector={Connector}, Status={Status}, ErrorCode={ErrorCode}",
            chargePointId, 
            request?.ConnectorId, 
            request?.Status,
            request?.ErrorCode);

        StatusNotificationResponse response = new StatusNotificationResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleDataTransfer(string chargePointId, JsonElement payload)
    {
        DataTransferRequest? request = JsonSerializer.Deserialize<DataTransferRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] DataTransfer: VendorId={VendorId}, MessageId={MessageId}",
            chargePointId, 
            request?.VendorId, 
            request?.MessageId);

        DataTransferResponse response = new DataTransferResponse
        {
            Status = "Accepted"
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleDiagnosticsStatusNotification(string chargePointId, JsonElement payload)
    {
        DiagnosticsStatusNotificationRequest? request = JsonSerializer.Deserialize<DiagnosticsStatusNotificationRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] DiagnosticsStatusNotification: Status={Status}",
            chargePointId, request?.Status);

        DiagnosticsStatusNotificationResponse response = new DiagnosticsStatusNotificationResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleFirmwareStatusNotification(string chargePointId, JsonElement payload)
    {
        FirmwareStatusNotificationRequest? request = JsonSerializer.Deserialize<FirmwareStatusNotificationRequest>(payload.GetRawText());
        
        _logger.LogInformation("[{ChargePointId}] FirmwareStatusNotification: Status={Status}",
            chargePointId, request?.Status);

        FirmwareStatusNotificationResponse response = new FirmwareStatusNotificationResponse();
        return Task.FromResult<object>(response);
    }
}
