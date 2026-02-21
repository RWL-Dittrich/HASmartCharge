using System.Net.WebSockets;
using HASmartCharge.Backend.OCPP.Services;

namespace HASmartCharge.Backend.OCPP.Transport;

/// <summary>
/// WebSocket implementation of IConnection
/// Wraps WebSocket transport details and delegates to WebSocketMessageService
/// </summary>
public class WebSocketConnection : IConnection
{
    private readonly WebSocket _webSocket;
    private readonly WebSocketMessageService _messageService;
    private readonly string _remoteEndPoint;

    public WebSocketConnection(
        WebSocket webSocket,
        string connectionId,
        string remoteEndPoint,
        WebSocketMessageService messageService)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
    }

    public string ConnectionId { get; }

    public string RemoteEndPoint => _remoteEndPoint;

    public bool IsOpen => _webSocket.State == WebSocketState.Open;

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Connection is not open");
        }

        await _messageService.SendMessageAsync(_webSocket, message, cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Server closing connection",
                cancellationToken);
        }
    }

    /// <summary>
    /// Receive the next message from the WebSocket
    /// Returns null if connection is closed
    /// </summary>
    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        return await _messageService.ReceiveMessageAsync(_webSocket, cancellationToken);
    }
}
