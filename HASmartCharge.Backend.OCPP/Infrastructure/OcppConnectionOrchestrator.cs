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
    private readonly ChargerStatusTracker _statusTracker;
    private readonly ChargerConfigurationService _configurationService;

    public OcppConnectionOrchestrator(
        ILogger<OcppConnectionOrchestrator> logger,
        ILoggerFactory loggerFactory,
        WebSocketMessageService messageService,
        ISessionManager sessionManager,
        IOcppMessageRouter messageRouter,
        ChargerStatusTracker statusTracker,
        ChargerConfigurationService configurationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _statusTracker = statusTracker ?? throw new ArgumentNullException(nameof(statusTracker));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
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
        string connectionId = Guid.NewGuid().ToString();
        WebSocketConnection connection = new WebSocketConnection(
            webSocket,
            connectionId,
            remoteEndPoint,
            _messageService);

        // Create session
        IChargePointSession session = new ChargePointSession(
            chargePointId,
            connection,
            _loggerFactory.CreateLogger<ChargePointSession>(),
            _statusTracker,
            _configurationService);

        // Register session
        _sessionManager.RegisterSession(session);

        try
        {
            // Initialize session (triggers BootNotification, configuration, etc.)
            await session.InitializeAsync(cancellationToken);

            // Process messages
            await ProcessMessagesAsync(connection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ChargePointId}] Error in connection", chargePointId);
        }
        finally
        {
            // Cleanup
            _sessionManager.UnregisterSession(chargePointId);
            await session.DisposeAsync();

            _logger.LogInformation("[{ChargePointId}] Connection closed", chargePointId);
        }
    }

    private async Task ProcessMessagesAsync(
        WebSocketConnection connection,
        CancellationToken cancellationToken)
    {
        while (connection.IsOpen && !cancellationToken.IsCancellationRequested)
        {
            // Receive message
            string? rawMessage = await connection.ReceiveAsync(cancellationToken);

            if (rawMessage == null)
            {
                // Connection closed
                break;
            }

            // Route message and get response
            string? response = await _messageRouter.RouteAsync(connection, rawMessage, cancellationToken);

            // Send response if needed
            if (!string.IsNullOrEmpty(response))
            {
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
