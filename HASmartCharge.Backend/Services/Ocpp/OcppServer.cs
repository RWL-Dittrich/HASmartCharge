using System.Net.WebSockets;
using System.Text;
using GoldsparkIT.OCPP;
using GoldsparkIT.OCPP.Models;
using GoldsparkIT.OCPP.Models.Enums;
using GoldsparkIT.OCPP.Models.Requests;
using GoldsparkIT.OCPP.Models.Responses;
using GoldsparkIT.OCPP.Models.Responses.Enums;
using Newtonsoft.Json;

namespace HASmartCharge.Backend.Services.Ocpp;

public class OcppServer
{
    private readonly string _chargePointId;
    private readonly ILogger<OcppServer> _logger;
    private readonly JsonServerHandler _ocppHandler;
    private readonly OcppWebSocketHandler _webSocketHandler;

    public OcppServer(string chargePointId, JsonServerHandler ocppHandler, ILogger<OcppServer> logger)
    {
        _chargePointId = chargePointId;
        _logger = logger;
        _ocppHandler = ocppHandler;

        _webSocketHandler = new OcppWebSocketHandler();
        _webSocketHandler.OnReceiveMessage += OnReceiveMessage;

        // Register all OCPP 1.6J message handlers
        _ocppHandler.OnSendMessage += SendMessage;
        _ocppHandler.OnAuthorize += OnAuthorize;
        _ocppHandler.OnBootNotification += OnBootNotification;
        _ocppHandler.OnDataTransfer += OnDataTransfer;
        _ocppHandler.OnDiagnosticsStatusNotification += OnDiagnosticsStatusNotification;
        _ocppHandler.OnFirmwareStatusNotification += OnFirmwareStatusNotification;
        _ocppHandler.OnHeartbeat += OnHeartbeat;
        _ocppHandler.OnMeterValues += OnMeterValues;
        _ocppHandler.OnStartTransaction += OnStartTransaction;
        _ocppHandler.OnStatusNotification += OnStatusNotification;
        _ocppHandler.OnStopTransaction += OnStopTransaction;
    }

    private Task<AuthorizeResponse> OnAuthorize(AuthorizeRequest request)
    {
        _logger.LogInformation("[{chargePointId}] Authorize request: {req}", _chargePointId, JsonConvert.SerializeObject(request));

        return Task.FromResult(new AuthorizeResponse(
            new IdTagInfo(IdTagInfoStatus.Accepted)
            {
                ExpiryDate = DateTimeOffset.Now.AddHours(24),
                ParentIdTag = null
            }
        ));
    }

    private Task<BootNotificationResponse> OnBootNotification(BootNotificationRequest request)
    {
        _logger.LogInformation("[{chargePointId}] BootNotification: Vendor={vendor}, Model={model}, Serial={serial}", 
            _chargePointId, 
            request.ChargePointVendor, 
            request.ChargePointModel, 
            request.ChargePointSerialNumber);

        return Task.FromResult(new BootNotificationResponse(
            BootNotificationResponseStatus.Accepted,
            DateTimeOffset.Now,
            60 // Heartbeat interval in seconds
        ));
    }

    private Task<DataTransferResponse> OnDataTransfer(DataTransferRequest request)
    {
        _logger.LogInformation("[{chargePointId}] DataTransfer: VendorId={vendorId}, MessageId={messageId}", 
            _chargePointId, 
            request.VendorId, 
            request.MessageId);

        return Task.FromResult(new DataTransferResponse(DataTransferResponseStatus.Accepted)
        {
            Data = ""
        });
    }

    private Task OnDiagnosticsStatusNotification(DiagnosticsStatusNotificationRequest request)
    {
        _logger.LogInformation("[{chargePointId}] DiagnosticsStatusNotification: Status={status}", 
            _chargePointId, 
            request.Status);

        return Task.CompletedTask;
    }

    private Task OnFirmwareStatusNotification(FirmwareStatusNotificationRequest request)
    {
        _logger.LogInformation("[{chargePointId}] FirmwareStatusNotification: Status={status}", 
            _chargePointId, 
            request.Status);

        return Task.CompletedTask;
    }

    private Task<HeartbeatResponse> OnHeartbeat(HeartbeatRequest request)
    {
        _logger.LogDebug("[{chargePointId}] Heartbeat received", _chargePointId);

        return Task.FromResult(new HeartbeatResponse(DateTimeOffset.Now));
    }

    private Task OnMeterValues(MeterValuesRequest request)
    {
        _logger.LogInformation("[{chargePointId}] MeterValues: ConnectorId={connectorId}, TransactionId={transactionId}, Values={values}", 
            _chargePointId, 
            request.ConnectorId, 
            request.TransactionId,
            request.MeterValue?.Count ?? 0);

        return Task.CompletedTask;
    }

    private Task<StartTransactionResponse> OnStartTransaction(StartTransactionRequest request)
    {
        _logger.LogInformation("[{chargePointId}] StartTransaction: ConnectorId={connectorId}, IdTag={idTag}, MeterStart={meterStart}", 
            _chargePointId, 
            request.ConnectorId, 
            request.IdTag,
            request.MeterStart);

        // Generate a unique transaction ID
        var transactionId = Random.Shared.Next(1, 1000000);

        return Task.FromResult(new StartTransactionResponse(
            new IdTagInfo(IdTagInfoStatus.Accepted)
            {
                ExpiryDate = DateTimeOffset.Now.AddHours(24),
                ParentIdTag = null
            },
            transactionId
        ));
    }

    private Task OnStatusNotification(StatusNotificationRequest request)
    {
        _logger.LogInformation("[{chargePointId}] StatusNotification: ConnectorId={connectorId}, Status={status}, ErrorCode={errorCode}", 
            _chargePointId, 
            request.ConnectorId, 
            request.Status,
            request.ErrorCode);

        return Task.CompletedTask;
    }

    private Task<StopTransactionResponse> OnStopTransaction(StopTransactionRequest request)
    {
        _logger.LogInformation("[{chargePointId}] StopTransaction: TransactionId={transactionId}, IdTag={idTag}, MeterStop={meterStop}, Reason={reason}", 
            _chargePointId, 
            request.TransactionId, 
            request.IdTag,
            request.MeterStop,
            request.Reason);

        return Task.FromResult(new StopTransactionResponse
        {
            IdTagInfo = new IdTagInfo(IdTagInfoStatus.Accepted)
            {
                ExpiryDate = DateTimeOffset.Now.AddHours(24),
                ParentIdTag = null
            }
        });
    }

    private async Task OnReceiveMessage(byte[] data)
    {
        var msg = Encoding.UTF8.GetString(data);
        _logger.LogDebug("[{chargePointId}] Received message: {msg}", _chargePointId, msg);

        await _ocppHandler.HandleMessage(msg);
    }

    private async Task SendMessage(string message)
    {
        _logger.LogDebug("[{chargePointId}] Sending message: {msg}", _chargePointId, message);
        await _webSocketHandler.Send(message);
    }

    public void Start(WebSocket webSocket)
    {
        _webSocketHandler.Start(webSocket);
    }
}
