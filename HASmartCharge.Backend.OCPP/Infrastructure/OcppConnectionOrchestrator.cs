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

            // Process messages
            await ProcessMessagesAsync(connection, chargePointId, cancellationToken);
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

    private async Task ProcessMessagesAsync(
        WebSocketConnection connection,
        string chargePointId,
        CancellationToken cancellationToken)
    {
        while (connection.IsOpen && !cancellationToken.IsCancellationRequested)
        {
            // Receive message
            var rawMessage = await connection.ReceiveAsync(cancellationToken);

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
