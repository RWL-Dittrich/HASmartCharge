using System.Collections.Concurrent;
using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.OCPP.Transport;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Domain;

/// <summary>
/// Represents a single connected charge point session.
/// Owns OCPP message handling for one charger: auto-accepts all inbound
/// transactions, forwards telemetry to <see cref="IChargerTelemetrySink"/>,
/// and exposes a small outbound command surface (availability, configuration).
/// </summary>
public class ChargePointSession : IChargePointSession
{
    private readonly ILogger<ChargePointSession> _logger;
    private readonly ChargerConfigurationService _configurationService;
    private readonly IChargerTelemetrySink _telemetry;
    private readonly IOcppChargerConfigurationProvider _configProvider;
    private int _messageIdCounter;
    private int _transactionIdCounter = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1_000_000);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<OcppCommandResult>> _pendingCommands = new();
    // Active transaction per connector, used to answer retransmitted StartTransaction requests
    // with the SAME transaction id instead of minting a new one (chargers retry when a reply is slow).
    private readonly ConcurrentDictionary<int, (int TransactionId, int MeterStart)> _activeTransactions = new();

    public ChargePointSession(
        string chargePointId,
        IConnection connection,
        ILogger<ChargePointSession> logger,
        ChargerConfigurationService configurationService,
        IChargerTelemetrySink telemetry,
        IOcppChargerConfigurationProvider configProvider)
    {
        ChargePointId = chargePointId ?? throw new ArgumentNullException(nameof(chargePointId));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));

        ConnectedAt = DateTime.UtcNow;
        IsActive = true;

        _logger.LogInformation("[{ChargePointId}] Session created", ChargePointId);
    }

    public string ChargePointId { get; }
    public IConnection Connection { get; }
    public bool IsActive { get; private set; }
    public DateTime ConnectedAt { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ChargePointId}] Initializing session", ChargePointId);

        _telemetry.OnConnected(ChargePointId);

        // Trigger BootNotification + push configuration in the background so it runs
        // in parallel with message processing. The orchestrator keeps the session alive.
        _ = InitializeInBackgroundAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task InitializeInBackgroundAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(2000, cancellationToken); // let the connection settle
            await TriggerBootNotificationAsync(cancellationToken);

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

    public async Task<object> HandleCallAsync(string action, JsonElement payload, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{ChargePointId}] Handling action: {Action}", ChargePointId, action);

        return action switch
        {
            "BootNotification" => await HandleBootNotificationAsync(payload, cancellationToken),
            "Heartbeat" => HandleHeartbeat(),
            "Authorize" => HandleAuthorize(payload),
            "StartTransaction" => HandleStartTransaction(payload),
            "StopTransaction" => HandleStopTransaction(payload),
            "MeterValues" => HandleMeterValues(payload),
            "StatusNotification" => HandleStatusNotification(payload),
            "DataTransfer" => HandleDataTransfer(payload),
            _ => throw new NotSupportedException($"Action '{action}' is not supported")
        };
    }

    public Task HandleCallResultAsync(string messageId, JsonElement payload, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{ChargePointId}] Received CallResult for message {MessageId}", ChargePointId, messageId);
        if (_pendingCommands.TryRemove(messageId, out var tcs))
            tcs.TrySetResult(OcppCommandResult.FromCallResult(payload));
        return Task.CompletedTask;
    }

    public Task HandleCallErrorAsync(string messageId, string errorCode, JsonElement payload, CancellationToken cancellationToken = default)
    {
        var errorDescription = payload.ValueKind == JsonValueKind.String ? payload.GetString() : null;
        _logger.LogWarning("[{ChargePointId}] Received CallError for message {MessageId}: {ErrorCode}", ChargePointId, messageId, errorCode);
        if (_pendingCommands.TryRemove(messageId, out var tcs))
            tcs.TrySetResult(OcppCommandResult.FromCallError(errorCode, errorDescription));
        return Task.CompletedTask;
    }

    #region Inbound OCPP Handlers (CP -> CS) — all auto-accepted

    private async Task<object> HandleBootNotificationAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<BootNotificationRequest>(payload.GetRawText());

        _logger.LogInformation("[{ChargePointId}] BootNotification: Vendor={Vendor}, Model={Model}, Serial={Serial}",
            ChargePointId, request?.ChargePointVendor, request?.ChargePointModel, request?.ChargePointSerialNumber);

        if (request != null)
        {
            _telemetry.OnBoot(ChargePointId, new ChargerInfo
            {
                Vendor = request.ChargePointVendor,
                Model = request.ChargePointModel,
                SerialNumber = request.ChargePointSerialNumber,
                FirmwareVersion = request.FirmwareVersion
            });
        }

        var config = await _configProvider.GetConfigurationAsync(ChargePointId, cancellationToken);

        return new BootNotificationResponse
        {
            Status = "Accepted",
            CurrentTime = DateTime.UtcNow,
            Interval = config.HeartbeatIntervalSeconds
        };
    }

    private object HandleHeartbeat()
    {
        _logger.LogDebug("[{ChargePointId}] Heartbeat received", ChargePointId);
        return new HeartbeatResponse { CurrentTime = DateTime.UtcNow };
    }

    private object HandleAuthorize(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<AuthorizeRequest>(payload.GetRawText());
        _logger.LogInformation("[{ChargePointId}] Authorize: IdTag={IdTag}", ChargePointId, request?.IdTag);

        // No id-tag whitelist — accept everything.
        return new AuthorizeResponse
        {
            IdTagInfo = new IdTagInfo { Status = "Accepted", ExpiryDate = DateTime.UtcNow.AddHours(24) }
        };
    }

    private object HandleStartTransaction(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<StartTransactionRequest>(payload.GetRawText());
        if (request is null)
            return new StartTransactionResponse { TransactionId = 0, IdTagInfo = new IdTagInfo { Status = "Invalid" } };

        // Retransmission of the transaction we already accepted on this connector:
        // answer with the same id and don't emit a second telemetry event.
        if (_activeTransactions.TryGetValue(request.ConnectorId, out var active) && active.MeterStart == request.MeterStart)
        {
            _logger.LogWarning(
                "[{ChargePointId}] Duplicate StartTransaction on connector {Connector} (MeterStart={MeterStart}); replying with existing TransactionId={TransactionId}",
                ChargePointId, request.ConnectorId, request.MeterStart, active.TransactionId);

            return new StartTransactionResponse
            {
                TransactionId = active.TransactionId,
                IdTagInfo = new IdTagInfo { Status = "Accepted", ExpiryDate = DateTime.UtcNow.AddHours(24) }
            };
        }

        var transactionId = Interlocked.Increment(ref _transactionIdCounter);
        _activeTransactions[request.ConnectorId] = (transactionId, request.MeterStart);

        _logger.LogInformation(
            "[{ChargePointId}] StartTransaction: Connector={Connector}, IdTag={IdTag}, MeterStart={MeterStart}, TransactionId={TransactionId}",
            ChargePointId, request.ConnectorId, request.IdTag, request.MeterStart, transactionId);

        _telemetry.OnTransactionStarted(
            ChargePointId, request.ConnectorId, transactionId, request.MeterStart, request.IdTag,
            new DateTimeOffset(request.Timestamp, TimeSpan.Zero));

        return new StartTransactionResponse
        {
            TransactionId = transactionId,
            IdTagInfo = new IdTagInfo { Status = "Accepted", ExpiryDate = DateTime.UtcNow.AddHours(24) }
        };
    }

    private object HandleStopTransaction(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<StopTransactionRequest>(payload.GetRawText());

        _logger.LogInformation(
            "[{ChargePointId}] StopTransaction: TransactionId={TransactionId}, IdTag={IdTag}, MeterStop={MeterStop}, Reason={Reason}",
            ChargePointId, request?.TransactionId, request?.IdTag, request?.MeterStop, request?.Reason);

        if (request != null)
        {
            foreach (var (connectorId, active) in _activeTransactions)
            {
                if (active.TransactionId == request.TransactionId)
                {
                    _activeTransactions.TryRemove(connectorId, out _);
                    break;
                }
            }

            _telemetry.OnTransactionStopped(
                ChargePointId, request.TransactionId, request.MeterStop, request.Reason,
                new DateTimeOffset(request.Timestamp, TimeSpan.Zero));
        }

        return new StopTransactionResponse { IdTagInfo = new IdTagInfo { Status = "Accepted" } };
    }

    private object HandleMeterValues(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<MeterValuesRequest>(payload.GetRawText());

        if (request != null)
        {
            _logger.LogInformation(
                "[{ChargePointId}] MeterValues: Connector={Connector}, TransactionId={TransactionId}, Values={ValueCount}",
                ChargePointId, request.ConnectorId, request.TransactionId, request.MeterValue?.Count ?? 0);

            _telemetry.OnMeterValues(ChargePointId, request);
        }

        return new MeterValuesResponse();
    }

    private object HandleStatusNotification(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<StatusNotificationRequest>(payload.GetRawText());

        _logger.LogInformation(
            "[{ChargePointId}] StatusNotification: Connector={Connector}, Status={Status}, ErrorCode={ErrorCode}",
            ChargePointId, request?.ConnectorId, request?.Status, request?.ErrorCode);

        if (request != null)
            _telemetry.OnConnectorStatus(ChargePointId, request.ConnectorId, request.Status ?? "Unknown", request.ErrorCode);

        return new StatusNotificationResponse();
    }

    private object HandleDataTransfer(JsonElement payload)
    {
        var request = JsonSerializer.Deserialize<DataTransferRequest>(payload.GetRawText());
        _logger.LogInformation("[{ChargePointId}] DataTransfer: VendorId={VendorId}, MessageId={MessageId}",
            ChargePointId, request?.VendorId, request?.MessageId);
        return new DataTransferResponse { Status = "Accepted" };
    }

    #endregion

    #region Outbound Commands (CS -> CP)

    public async Task<OcppCommandResult> SendCommandAsync<TRequest>(string action, TRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsActive || !Connection.IsOpen)
        {
            _logger.LogWarning("[{ChargePointId}] Cannot send command, connection not active", ChargePointId);
            return OcppCommandResult.FromCallError("ConnectionError", "Connection is not active");
        }

        var messageId = string.Empty;
        TaskCompletionSource<OcppCommandResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            messageId = Interlocked.Increment(ref _messageIdCounter).ToString();

            var message = new OcppMessage
            {
                MessageType = (int)OcppMessageType.Call,
                MessageId = messageId,
                Action = action,
                Payload = JsonSerializer.SerializeToElement(request)
            };

            var messageJson = message.ToJson();
            _logger.LogInformation("[{ChargePointId}] Sending {Action} command: {Message}", ChargePointId, action, messageJson);

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

        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _pendingCommands.TryRemove(messageId, out _);
            _logger.LogWarning("[{ChargePointId}] Command {Action} timed out (message {MessageId})", ChargePointId, action, messageId);
            return OcppCommandResult.TimedOut();
        }
    }

    public Task<OcppCommandResult> SetAvailabilityAsync(int connectorId, bool available, CancellationToken cancellationToken = default) =>
        SendCommandAsync("ChangeAvailability", new ChangeAvailabilityRequest
        {
            ConnectorId = connectorId,
            Type = available ? "Operative" : "Inoperative"
        }, cancellationToken);

    public Task<OcppCommandResult> ChangeConfigurationAsync(string key, string value, CancellationToken cancellationToken = default) =>
        SendCommandAsync("ChangeConfiguration", new ChangeConfigurationRequest { Key = key, Value = value }, cancellationToken);

    private async Task<bool> TriggerBootNotificationAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync("TriggerMessage", new TriggerMessageRequest { RequestedMessage = "BootNotification" }, cancellationToken);
        return result.Success;
    }

    #endregion

    public async Task DisposeAsync()
    {
        _logger.LogInformation("[{ChargePointId}] Disposing session", ChargePointId);
        IsActive = false;
        _telemetry.OnDisconnected(ChargePointId);

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
