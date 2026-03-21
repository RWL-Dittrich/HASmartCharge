using System.Collections.Concurrent;
using System.Text.Json;
using HASmartCharge.Application.Commands;
using HASmartCharge.Application.Events;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.OCPP.Transport;
using HASmartCharge.Domain.Events;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Sessions;

/// <summary>
/// Represents a single connected charge point session
/// Owns all charge point state, configuration, and OCPP message handling logic
/// </summary>
public class ChargePointSession : IChargePointSession
{
    private readonly ILogger<ChargePointSession> _logger;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ChargerConfigurationService _configurationService;
    private readonly RegisterChargerHandler _registerChargerHandler;
    private readonly BeginChargingSessionHandler _beginChargingSessionHandler;
    private readonly CompleteChargingSessionHandler _completeChargingSessionHandler;
    private readonly UpdateConnectorStatusHandler _updateConnectorStatusHandler;
    private int _messageIdCounter;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<OcppCommandResult>> _pendingCommands = new();

    public ChargePointSession(
        string chargePointId,
        IConnection connection,
        ILogger<ChargePointSession> logger,
        IDomainEventDispatcher dispatcher,
        ChargerConfigurationService configurationService,
        RegisterChargerHandler registerChargerHandler,
        BeginChargingSessionHandler beginChargingSessionHandler,
        CompleteChargingSessionHandler completeChargingSessionHandler,
        UpdateConnectorStatusHandler updateConnectorStatusHandler)
    {
        ChargePointId = chargePointId ?? throw new ArgumentNullException(nameof(chargePointId));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _registerChargerHandler = registerChargerHandler ?? throw new ArgumentNullException(nameof(registerChargerHandler));
        _beginChargingSessionHandler = beginChargingSessionHandler ?? throw new ArgumentNullException(nameof(beginChargingSessionHandler));
        _completeChargingSessionHandler = completeChargingSessionHandler ?? throw new ArgumentNullException(nameof(completeChargingSessionHandler));
        _updateConnectorStatusHandler = updateConnectorStatusHandler ?? throw new ArgumentNullException(nameof(updateConnectorStatusHandler));

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

        // Dispatch connection event so the read model (tracker) is updated
        await _dispatcher.DispatchAsync(new ChargerConnected(ChargePointId, DateTimeOffset.UtcNow), cancellationToken);

        // Trigger BootNotification and configuration in background
        // Note: Not awaited intentionally - initialization continues in parallel with message processing
        // The session lifecycle is managed by the orchestrator, which keeps the session alive
        _ = InitializeInBackgroundAsync(cancellationToken);
    }

    private async Task InitializeInBackgroundAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Small delay to ensure connection is stable
            await Task.Delay(2000, cancellationToken);

            await TriggerBootNotificationAsync(cancellationToken);

            // Configure charger after boot notification
            await Task.Delay(2000, cancellationToken);
            await _configurationService.ConfigureChargerAsync(ChargePointId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[{ChargePointId}] Background initialization cancelled", ChargePointId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error in background initialization", ChargePointId);
        }
    }

    public async Task<object> HandleCallAsync(
        string action,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{ChargePointId}] Handling action: {Action}", ChargePointId, action);

        return action switch
        {
            "BootNotification" => await HandleBootNotificationAsync(payload, cancellationToken),
            "Heartbeat" => await HandleHeartbeatAsync(payload),
            "Authorize" => await HandleAuthorizeAsync(payload),
            "StartTransaction" => await HandleStartTransactionAsync(payload, cancellationToken),
            "StopTransaction" => await HandleStopTransactionAsync(payload, cancellationToken),
            "MeterValues" => await HandleMeterValuesAsync(payload),
            "StatusNotification" => await HandleStatusNotificationAsync(payload, cancellationToken),
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

        if (_pendingCommands.TryRemove(messageId, out TaskCompletionSource<OcppCommandResult>? tcs))
        {
            tcs.TrySetResult(OcppCommandResult.FromCallResult(payload));
        }

        return Task.CompletedTask;
    }

    public Task HandleCallErrorAsync(
        string messageId,
        string errorCode,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        string? errorDescription = payload.ValueKind == JsonValueKind.String
            ? payload.GetString()
            : null;

        _logger.LogWarning("[{ChargePointId}] Received CallError for message {MessageId}: {ErrorCode}",
            ChargePointId, messageId, errorCode);

        if (_pendingCommands.TryRemove(messageId, out TaskCompletionSource<OcppCommandResult>? tcs))
        {
            tcs.TrySetResult(OcppCommandResult.FromCallError(errorCode, errorDescription));
        }

        return Task.CompletedTask;
    }

    #region Inbound OCPP Handlers (CP -> CS)

    private async Task<object> HandleBootNotificationAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        BootNotificationRequest? request = JsonSerializer.Deserialize<BootNotificationRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] BootNotification: Vendor={Vendor}, Model={Model}, Serial={Serial}",
            ChargePointId,
            request?.ChargePointVendor,
            request?.ChargePointModel,
            request?.ChargePointSerialNumber);

        if (request != null)
        {
            string vendor = request.ChargePointVendor ?? "";
            string model = request.ChargePointModel ?? "";
            string? serial = request.ChargePointSerialNumber;
            string? firmware = request.FirmwareVersion;

            // Persist via application command handler (TASK-017)
            try
            {
                await _registerChargerHandler.HandleAsync(new RegisterChargerCommand(
                    ChargePointId, vendor, model, serial, firmware), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ChargePointId}] Failed to persist boot notification", ChargePointId);
            }

            // Dispatch domain event so read model (tracker) is updated (TASK-022)
            await _dispatcher.DispatchAsync(new ChargerRegistered(
                ChargePointId, vendor, model, serial, firmware, DateTimeOffset.UtcNow), cancellationToken);
        }

        BootNotificationResponse response = new BootNotificationResponse
        {
            Status = "Accepted",
            CurrentTime = DateTime.UtcNow,
            Interval = 60
        };

        return response;
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

    private async Task<object> HandleStartTransactionAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        StartTransactionRequest? request = JsonSerializer.Deserialize<StartTransactionRequest>(payload.GetRawText());

        if (request is null)
        {
            return new StartTransactionResponse
            {
                TransactionId = 0,
                IdTagInfo = new IdTagInfo { Status = "Invalid" }
            };
        }

        // Get a stable transaction ID from the application layer (TASK-018)
        int transactionId;
        try
        {
            transactionId = await _beginChargingSessionHandler.HandleAsync(new BeginChargingSessionCommand(
                ChargePointId,
                request.ConnectorId,
                request.IdTag,
                request.MeterStart,
                new DateTimeOffset(request.Timestamp, TimeSpan.Zero)), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Failed to persist StartTransaction", ChargePointId);
            // Fallback: still accept the transaction but log the error
            transactionId = Environment.TickCount;
        }

        _logger.LogInformation(
            "[{ChargePointId}] StartTransaction: Connector={Connector}, IdTag={IdTag}, MeterStart={MeterStart}, TransactionId={TransactionId}",
            ChargePointId,
            request.ConnectorId,
            request.IdTag,
            request.MeterStart,
            transactionId);

        // Dispatch domain event so read model (tracker) is updated (TASK-022)
        await _dispatcher.DispatchAsync(new ChargingSessionStarted(
            transactionId, ChargePointId, request.ConnectorId, request.IdTag,
            request.MeterStart, DateTimeOffset.UtcNow), cancellationToken);

        StartTransactionResponse response = new StartTransactionResponse
        {
            TransactionId = transactionId,
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted",
                ExpiryDate = DateTime.UtcNow.AddHours(24)
            }
        };

        return response;
    }

    private async Task<object> HandleStopTransactionAsync(JsonElement payload, CancellationToken cancellationToken)
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
            // Persist via application command handler (TASK-019)
            try
            {
                await _completeChargingSessionHandler.HandleAsync(new CompleteChargingSessionCommand(
                    request.TransactionId,
                    request.MeterStop,
                    request.Reason,
                    new DateTimeOffset(request.Timestamp, TimeSpan.Zero)), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ChargePointId}] Failed to persist StopTransaction {TransactionId}",
                    ChargePointId, request.TransactionId);
            }

            // Dispatch domain event so read model (tracker) is updated (TASK-022)
            // ConnectorId is not in StopTransactionRequest; pass 0 as approximation
            await _dispatcher.DispatchAsync(new ChargingSessionCompleted(
                request.TransactionId, ChargePointId, 0,
                request.MeterStop, request.Reason, DateTimeOffset.UtcNow), cancellationToken);
        }

        StopTransactionResponse response = new StopTransactionResponse
        {
            IdTagInfo = new IdTagInfo
            {
                Status = "Accepted"
            }
        };

        return response;
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
                foreach (MeterValue meterValue in request.MeterValue)
                {
                    foreach (SampledValue sampledValue in meterValue.SampledValue)
                    {
                        string measurand = sampledValue.Measurand ?? "Energy.Active.Import.Register";
                        string phase = sampledValue.Phase != null ? $" (Phase: {sampledValue.Phase})" : "";
                        _logger.LogDebug("[{ChargePointId}] Measurand: {Measurand}{Phase} = {Value} {Unit}",
                            ChargePointId, measurand, phase, sampledValue.Value, sampledValue.Unit ?? "");
                    }
                }
            }

            // Meter values go directly to the tracker for now (TASK-021/022 stretch goal)
            // A MeterValuesReported domain event could be dispatched here in a future iteration
        }

        MeterValuesResponse response = new MeterValuesResponse();
        return Task.FromResult<object>(response);
    }

    private async Task<object> HandleStatusNotificationAsync(JsonElement payload, CancellationToken cancellationToken)
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
            // Persist connector to DB via application command (only real connectors, not connector 0 which is the charger itself)
            // (TASK-020)
            if (request.ConnectorId > 0)
            {
                try
                {
                    await _updateConnectorStatusHandler.HandleAsync(new UpdateConnectorStatusCommand(
                        ChargePointId, request.ConnectorId, request.Status ?? "Unknown", request.ErrorCode), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{ChargePointId}] Failed to persist connector {ConnectorId} status",
                        ChargePointId, request.ConnectorId);
                }
            }

            // Dispatch domain event so read model (tracker) is updated (TASK-022)
            await _dispatcher.DispatchAsync(new ConnectorStatusUpdated(
                ChargePointId, request.ConnectorId, request.Status ?? "Unknown",
                request.ErrorCode, DateTimeOffset.UtcNow), cancellationToken);
        }

        StatusNotificationResponse response = new StatusNotificationResponse();
        return response;
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

    public async Task<OcppCommandResult> SendCommandAsync<TRequest>(
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActive || !Connection.IsOpen)
        {
            _logger.LogWarning("[{ChargePointId}] Cannot send command, connection not active", ChargePointId);
            return OcppCommandResult.FromCallError("ConnectionError", "Connection is not active");
        }

        string messageId = string.Empty;
        TaskCompletionSource<OcppCommandResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            messageId = Interlocked.Increment(ref _messageIdCounter).ToString();

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

            _pendingCommands[messageId] = tcs;

            await Connection.SendAsync(messageJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _pendingCommands.TryRemove(messageId, out _);
            _logger.LogError(ex, "[{ChargePointId}] Error sending command", ChargePointId);
            return OcppCommandResult.FromCallError("SendError", ex.Message);
        }
        finally
        {
            _sendLock.Release();
        }

        // Await the charger's response with a timeout
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _pendingCommands.TryRemove(messageId, out _);
            _logger.LogWarning("[{ChargePointId}] Command {Action} timed out (message {MessageId})",
                ChargePointId, action, messageId);
            return OcppCommandResult.TimedOut();
        }
    }

    public async Task<OcppCommandResult> SetAvailabilityAsync(
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

    public async Task<OcppCommandResult> RemoteStartTransactionAsync(
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

    public async Task<OcppCommandResult> RemoteStopTransactionAsync(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        RemoteStopTransactionRequest request = new RemoteStopTransactionRequest
        {
            TransactionId = transactionId
        };

        return await SendCommandAsync("RemoteStopTransaction", request, cancellationToken);
    }

    public async Task<OcppCommandResult> ChangeConfigurationAsync(
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

        OcppCommandResult result = await SendCommandAsync("TriggerMessage", request, cancellationToken);
        return result.Success;
    }

    #endregion

    public async Task DisposeAsync()
    {
        _logger.LogInformation("[{ChargePointId}] Disposing session", ChargePointId);

        IsActive = false;

        // Dispatch disconnection event so the read model (tracker) is updated
        await _dispatcher.DispatchAsync(new ChargerDisconnected(ChargePointId, DateTimeOffset.UtcNow));

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
