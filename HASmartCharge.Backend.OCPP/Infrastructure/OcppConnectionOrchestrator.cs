using System.Net.WebSockets;
using HASmartCharge.Backend.OCPP.Application;
using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.OCPP.Transport;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Infrastructure;

/// <summary>
/// Orchestrates the connection of charge points and message processing
/// Uses the new layered architecture: Transport -> Router -> Session
/// </summary>
public class OcppConnectionOrchestrator
{
    private readonly ILogger<OcppConnectionOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WebSocketMessageService _messageService;
    private readonly ISessionManager _sessionManager;
    private readonly IOcppMessageRouter _messageRouter;
    private readonly ChargerConfigurationService _configurationService;
    private readonly IChargerTelemetrySink _telemetry;
    private readonly IOcppChargerConfigurationProvider _configProvider;

    public OcppConnectionOrchestrator(
        ILogger<OcppConnectionOrchestrator> logger,
        ILoggerFactory loggerFactory,
        WebSocketMessageService messageService,
        ISessionManager sessionManager,
        IOcppMessageRouter messageRouter,
        ChargerConfigurationService configurationService,
        IChargerTelemetrySink telemetry,
        IOcppChargerConfigurationProvider configProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// Handle a WebSocket connection for a charge point
    /// Creates session, processes messages, and cleans up on disconnect
    /// </summary>
    public async Task HandleConnectionAsync(
        WebSocket webSocket,
        string chargePointId,
        string remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[{ChargePointId}] New connection from {RemoteEndPoint}",
            chargePointId,
            remoteEndPoint);

        // Create transport connection
        var connectionId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection(
            webSocket,
            connectionId,
            remoteEndPoint,
            _messageService);

        // Create session
        IChargePointSession session = new ChargePointSession(
            chargePointId,
            connection,
            _loggerFactory.CreateLogger<ChargePointSession>(),
            _configurationService,
            _telemetry,
            _configProvider);

        // Register session. A displaced previous session means the charger reconnected
        // while its old socket still looked open — abort that socket so its receive loop
        // unwinds now instead of waiting minutes for a TCP reset (zombie connection).
        var displaced = _sessionManager.RegisterSession(session);
        if (displaced != null)
        {
            _logger.LogWarning(
                "[{ChargePointId}] Reconnect while previous connection {ConnectionId} still registered — aborting stale connection",
                chargePointId,
                displaced.Connection.ConnectionId);
            try
            {
                displaced.Connection.Abort();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{ChargePointId}] Stale connection abort failed (already torn down)", chargePointId);
            }
        }

        try
        {
            // Initialize session (triggers BootNotification, configuration, etc.)
            await session.InitializeAsync(cancellationToken);

            // Process messages, aborting the link if OCPP traffic stops for the idle window.
            var idleTimeout = await ResolveIdleTimeoutAsync(chargePointId, cancellationToken);
            await ProcessMessagesAsync(connection, chargePointId, idleTimeout, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error in connection", chargePointId);
        }
        finally
        {
            // Cleanup. Only notify disconnected if this session is still the current
            // registration — a reconnect may have already replaced it, in which case
            // this stale teardown must not clobber the live session nor flip its status.
            var wasCurrent = _sessionManager.UnregisterSession(session);
            await session.DisposeAsync(notifyDisconnected: wasCurrent);

            _logger.LogInformation("[{ChargePointId}] Connection closed", chargePointId);
        }
    }

    // A charger emits an OCPP Heartbeat at least every HeartbeatInterval (plus MeterValues
    // during a transaction), so a healthy link is never silent for long. Allow 3 missed
    // heartbeats before declaring the link dead, with a floor so a tiny configured interval
    // can't cause false aborts on normal jitter.
    private async Task<TimeSpan> ResolveIdleTimeoutAsync(string chargePointId, CancellationToken cancellationToken)
    {
        var heartbeatSeconds = OcppChargerConfiguration.Default.HeartbeatIntervalSeconds;
        try
        {
            var config = await _configProvider.GetConfigurationAsync(chargePointId, cancellationToken);
            heartbeatSeconds = config.HeartbeatIntervalSeconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{ChargePointId}] Could not resolve heartbeat interval for idle timeout; using default",
                chargePointId);
        }

        return TimeSpan.FromSeconds(Math.Max(heartbeatSeconds * 3, 90));
    }

    private async Task ProcessMessagesAsync(
        WebSocketConnection connection,
        string chargePointId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken)
    {
        while (connection.IsOpen && !cancellationToken.IsCancellationRequested)
        {
            // Receive message. Bound the wait by the idle timeout: an OCPP-level silence
            // (no Heartbeat, no MeterValues) means the link is dead even though the TCP
            // socket may still look open, so abort it rather than block here forever.
            string? rawMessage;
            using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                idleCts.CancelAfter(idleTimeout);
                try
                {
                    rawMessage = await connection.ReceiveAsync(idleCts.Token);
                }
                catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "[{ChargePointId}] No OCPP traffic for {IdleSeconds}s — treating link as dead and aborting",
                        chargePointId,
                        (int)idleTimeout.TotalSeconds);
                    connection.Abort();
                    break;
                }
            }

            if (rawMessage == null)
            {
                // Connection closed
                break;
            }

            OcppRawLog.Append(chargePointId, "in", rawMessage);

            // Route message and get response
            var response = await _messageRouter.RouteAsync(connection, rawMessage, cancellationToken);

            // Send response if needed
            if (!string.IsNullOrEmpty(response))
            {
                OcppRawLog.Append(chargePointId, "out", response);
                await connection.SendAsync(response, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Get a charge point session by ID
    /// </summary>
    public IChargePointSession? GetSession(string chargePointId)
    {
        return _sessionManager.GetByChargePointId(chargePointId);
    }

    /// <summary>
    /// Check if a charge point is connected
    /// </summary>
    public bool IsConnected(string chargePointId)
    {
        return _sessionManager.IsConnected(chargePointId);
    }

    /// <summary>
    /// Get all connected charge point IDs
    /// </summary>
    public IEnumerable<string> GetConnectedChargePoints()
    {
        return _sessionManager.GetConnectedChargePointIds();
    }
}
