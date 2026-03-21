using System.Net.WebSockets;
using HASmartCharge.Application.Commands;
using HASmartCharge.Application.Events;
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
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ChargerConfigurationService _configurationService;
    private readonly RegisterChargerHandler _registerChargerHandler;
    private readonly BeginChargingSessionHandler _beginChargingSessionHandler;
    private readonly CompleteChargingSessionHandler _completeChargingSessionHandler;
    private readonly UpdateConnectorStatusHandler _updateConnectorStatusHandler;

    public OcppConnectionOrchestrator(
        ILogger<OcppConnectionOrchestrator> logger,
        ILoggerFactory loggerFactory,
        WebSocketMessageService messageService,
        ISessionManager sessionManager,
        IOcppMessageRouter messageRouter,
        IDomainEventDispatcher dispatcher,
        ChargerConfigurationService configurationService,
        RegisterChargerHandler registerChargerHandler,
        BeginChargingSessionHandler beginChargingSessionHandler,
        CompleteChargingSessionHandler completeChargingSessionHandler,
        UpdateConnectorStatusHandler updateConnectorStatusHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _registerChargerHandler = registerChargerHandler ?? throw new ArgumentNullException(nameof(registerChargerHandler));
        _beginChargingSessionHandler = beginChargingSessionHandler ?? throw new ArgumentNullException(nameof(beginChargingSessionHandler));
        _completeChargingSessionHandler = completeChargingSessionHandler ?? throw new ArgumentNullException(nameof(completeChargingSessionHandler));
        _updateConnectorStatusHandler = updateConnectorStatusHandler ?? throw new ArgumentNullException(nameof(updateConnectorStatusHandler));
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
            _dispatcher,
            _configurationService,
            _registerChargerHandler,
            _beginChargingSessionHandler,
            _completeChargingSessionHandler,
            _updateConnectorStatusHandler);

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
