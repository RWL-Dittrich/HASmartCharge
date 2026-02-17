using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.OCPP.Transport;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Domain;

/// <summary>
/// Represents a single connected charge point session
/// Owns all charge point state, configuration, and OCPP message handling logic
/// </summary>
public class ChargePointSession : IChargePointSession
{
    private readonly ILogger<ChargePointSession> _logger;
    private readonly ChargerStatusTracker _statusTracker;
    private readonly ChargerConfigurationService _configurationService;
    private int _transactionCounter;
    private int _messageIdCounter;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ChargePointSession(
        string chargePointId,
        IConnection connection,
        ILogger<ChargePointSession> logger,
        ChargerStatusTracker statusTracker,
        ChargerConfigurationService configurationService)
    {
        ChargePointId = chargePointId ?? throw new ArgumentNullException(nameof(chargePointId));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statusTracker = statusTracker ?? throw new ArgumentNullException(nameof(statusTracker));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        
        ConnectedAt = DateTime.UtcNow;
        IsActive = true;
        
        _logger.LogInformation("[{ChargePointId}] Session created", ChargePointId);
    }

    public string ChargePointId { get; }
    public IConnection Connection { get; }
    public bool IsActive { get; private set; }
    public DateTime ConnectedAt { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Initializing session", ChargePointId);
        
        // Update status tracker
        _statusTracker.OnChargerConnected(ChargePointId);
        
        // Trigger BootNotification after a short delay to ensure connection is stable
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, cancellationToken);
                await TriggerBootNotificationAsync(cancellationToken);
                
                // Configure charger after boot notification
                await Task.Delay(2000, cancellationToken);
                await _configurationService.ConfigureChargerAsync(ChargePointId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ChargePointId}] Error in initialization sequence", ChargePointId);
            }
        }, cancellationToken);
    }

    public async Task<object> HandleCallAsync(
        string action,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{ChargePointId}] Handling action: {Action}", ChargePointId, action);

        return action switch
        {
            "BootNotification" => await HandleBootNotificationAsync(payload),
            "Heartbeat" => await HandleHeartbeatAsync(payload),
            "Authorize" => await HandleAuthorizeAsync(payload),
            "StartTransaction" => await HandleStartTransactionAsync(payload),
            "StopTransaction" => await HandleStopTransactionAsync(payload),
            "MeterValues" => await HandleMeterValuesAsync(payload),
            "StatusNotification" => await HandleStatusNotificationAsync(payload),
            "DataTransfer" => await HandleDataTransferAsync(payload),
            "DiagnosticsStatusNotification" => await HandleDiagnosticsStatusNotificationAsync(payload),
            "FirmwareStatusNotification" => await HandleFirmwareStatusNotificationAsync(payload),
            _ => throw new NotSupportedException($"Action '{action}' is not supported")
        };
    }

    public Task HandleCallResultAsync(
        string messageId,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{ChargePointId}] Received CallResult for message {MessageId}",
            ChargePointId, messageId);
        
        // TODO: Implement correlation tracking for request/response matching if needed
        return Task.CompletedTask;
    }

    public Task HandleCallErrorAsync(
        string messageId,
        string errorCode,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[{ChargePointId}] Received CallError for message {MessageId}: {ErrorCode}",
            ChargePointId, messageId, errorCode);
        
        // TODO: Implement correlation tracking for request/response matching if needed
        return Task.CompletedTask;
    }

    #region Inbound OCPP Handlers (CP -> CS)

    private Task<object> HandleBootNotificationAsync(JsonElement payload)
    {
        BootNotificationRequest? request = JsonSerializer.Deserialize<BootNotificationRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] BootNotification: Vendor={Vendor}, Model={Model}, Serial={Serial}",
            ChargePointId,
            request?.ChargePointVendor,
            request?.ChargePointModel,
            request?.ChargePointSerialNumber);

        if (request != null)
        {
            _statusTracker.OnBootNotification(ChargePointId, request);
        }

        BootNotificationResponse response = new BootNotificationResponse
        {
            Status = "Accepted",
            CurrentTime = DateTime.UtcNow,
            Interval = 60
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleHeartbeatAsync(JsonElement payload)
    {
        _logger.LogDebug("[{ChargePointId}] Heartbeat received", ChargePointId);

        HeartbeatResponse response = new HeartbeatResponse
        {
            CurrentTime = DateTime.UtcNow
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleAuthorizeAsync(JsonElement payload)
    {
        AuthorizeRequest? request = JsonSerializer.Deserialize<AuthorizeRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] Authorize: IdTag={IdTag}",
            ChargePointId, request?.IdTag);

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

    private Task<object> HandleStartTransactionAsync(JsonElement payload)
    {
        StartTransactionRequest? request = JsonSerializer.Deserialize<StartTransactionRequest>(payload.GetRawText());

        int transactionId = Interlocked.Increment(ref _transactionCounter);

        _logger.LogInformation(
            "[{ChargePointId}] StartTransaction: Connector={Connector}, IdTag={IdTag}, MeterStart={MeterStart}, TransactionId={TransactionId}",
            ChargePointId,
            request?.ConnectorId,
            request?.IdTag,
            request?.MeterStart,
            transactionId);

        if (request != null)
        {
            _statusTracker.OnStartTransaction(ChargePointId, request, transactionId);
        }

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

    private Task<object> HandleStopTransactionAsync(JsonElement payload)
    {
        StopTransactionRequest? request = JsonSerializer.Deserialize<StopTransactionRequest>(payload.GetRawText());

        _logger.LogInformation(
            "[{ChargePointId}] StopTransaction: TransactionId={TransactionId}, IdTag={IdTag}, MeterStop={MeterStop}, Reason={Reason}",
            ChargePointId,
            request?.TransactionId,
            request?.IdTag,
            request?.MeterStop,
            request?.Reason);

        if (request != null)
        {
            _statusTracker.OnStopTransaction(ChargePointId, request);
        }

        StopTransactionResponse response = new StopTransactionResponse
        {
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted"
            }
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleMeterValuesAsync(JsonElement payload)
    {
        MeterValuesRequest? request = JsonSerializer.Deserialize<MeterValuesRequest>(payload.GetRawText());

        if (request != null)
        {
            _logger.LogInformation(
                "[{ChargePointId}] MeterValues: Connector={Connector}, TransactionId={TransactionId}, Values={ValueCount}",
                ChargePointId,
                request.ConnectorId,
                request.TransactionId,
                request.MeterValue?.Count ?? 0);

            if (request.MeterValue != null)
            {
                foreach (var meterValue in request.MeterValue)
                {
                    foreach (var sampledValue in meterValue.SampledValue)
                    {
                        string measurand = sampledValue.Measurand ?? "Energy.Active.Import.Register";
                        string phase = sampledValue.Phase != null ? $" (Phase: {sampledValue.Phase})" : "";
                        _logger.LogDebug("[{ChargePointId}] Measurand: {Measurand}{Phase} = {Value} {Unit}",
                            ChargePointId, measurand, phase, sampledValue.Value, sampledValue.Unit ?? "");
                    }
                }
            }

            _statusTracker.OnMeterValues(ChargePointId, request);
        }

        MeterValuesResponse response = new MeterValuesResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleStatusNotificationAsync(JsonElement payload)
    {
        StatusNotificationRequest? request = JsonSerializer.Deserialize<StatusNotificationRequest>(payload.GetRawText());

        _logger.LogInformation(
            "[{ChargePointId}] StatusNotification: Connector={Connector}, Status={Status}, ErrorCode={ErrorCode}",
            ChargePointId,
            request?.ConnectorId,
            request?.Status,
            request?.ErrorCode);

        if (request != null)
        {
            _statusTracker.OnStatusNotification(ChargePointId, request);
        }

        StatusNotificationResponse response = new StatusNotificationResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleDataTransferAsync(JsonElement payload)
    {
        DataTransferRequest? request = JsonSerializer.Deserialize<DataTransferRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] DataTransfer: VendorId={VendorId}, MessageId={MessageId}",
            ChargePointId,
            request?.VendorId,
            request?.MessageId);

        DataTransferResponse response = new DataTransferResponse
        {
            Status = "Accepted"
        };

        return Task.FromResult<object>(response);
    }

    private Task<object> HandleDiagnosticsStatusNotificationAsync(JsonElement payload)
    {
        DiagnosticsStatusNotificationRequest? request =
            JsonSerializer.Deserialize<DiagnosticsStatusNotificationRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] DiagnosticsStatusNotification: Status={Status}",
            ChargePointId, request?.Status);

        DiagnosticsStatusNotificationResponse response = new DiagnosticsStatusNotificationResponse();
        return Task.FromResult<object>(response);
    }

    private Task<object> HandleFirmwareStatusNotificationAsync(JsonElement payload)
    {
        FirmwareStatusNotificationRequest? request =
            JsonSerializer.Deserialize<FirmwareStatusNotificationRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] FirmwareStatusNotification: Status={Status}",
            ChargePointId, request?.Status);

        FirmwareStatusNotificationResponse response = new FirmwareStatusNotificationResponse();
        return Task.FromResult<object>(response);
    }

    #endregion

    #region Outbound Commands (CS -> CP)

    public async Task<bool> SendCommandAsync<TRequest>(
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActive || !Connection.IsOpen)
        {
            _logger.LogWarning("[{ChargePointId}] Cannot send command, connection not active", ChargePointId);
            return false;
        }

        await _sendLock.WaitAsync(cancellationToken);
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

            _logger.LogInformation("[{ChargePointId}] Sending {Action} command: {Message}",
                ChargePointId, action, messageJson);

            await Connection.SendAsync(messageJson, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error sending command", ChargePointId);
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<bool> SetAvailabilityAsync(
        int connectorId,
        bool available,
        CancellationToken cancellationToken = default)
    {
        ChangeAvailabilityRequest request = new ChangeAvailabilityRequest
        {
            ConnectorId = connectorId,
            Type = available ? "Operative" : "Inoperative"
        };

        return await SendCommandAsync("ChangeAvailability", request, cancellationToken);
    }

    public async Task<bool> RemoteStartTransactionAsync(
        int connectorId,
        string idTag,
        CancellationToken cancellationToken = default)
    {
        RemoteStartTransactionRequest request = new RemoteStartTransactionRequest
        {
            ConnectorId = connectorId,
            IdTag = idTag
        };

        return await SendCommandAsync("RemoteStartTransaction", request, cancellationToken);
    }

    public async Task<bool> RemoteStopTransactionAsync(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        RemoteStopTransactionRequest request = new RemoteStopTransactionRequest
        {
            TransactionId = transactionId
        };

        return await SendCommandAsync("RemoteStopTransaction", request, cancellationToken);
    }

    public async Task<bool> ChangeConfigurationAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ChangeConfigurationRequest request = new ChangeConfigurationRequest
        {
            Key = key,
            Value = value
        };

        return await SendCommandAsync("ChangeConfiguration", request, cancellationToken);
    }

    private async Task<bool> TriggerBootNotificationAsync(CancellationToken cancellationToken = default)
    {
        TriggerMessageRequest request = new TriggerMessageRequest
        {
            RequestedMessage = "BootNotification"
        };

        return await SendCommandAsync("TriggerMessage", request, cancellationToken);
    }

    #endregion

    public async Task DisposeAsync()
    {
        _logger.LogInformation("[{ChargePointId}] Disposing session", ChargePointId);
        
        IsActive = false;
        _statusTracker.OnChargerDisconnected(ChargePointId);
        
        try
        {
            await Connection.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error closing connection", ChargePointId);
        }
        
        _sendLock.Dispose();
    }
}
