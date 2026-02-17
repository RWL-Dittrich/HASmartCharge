using System.Net.WebSockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service for handling WebSocket message buffering and communication
/// </summary>
public class WebSocketMessageService
{
    private readonly ILogger<WebSocketMessageService> _logger;
    
    public WebSocketMessageService(ILogger<WebSocketMessageService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Receives a complete message from the WebSocket
    /// </summary>
    public async Task<string?> ReceiveMessageAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[4096];
        List<byte> messageBuffer = new List<byte>();

        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                    "Client closing connection", cancellationToken);
                return null;
            }

            messageBuffer.AddRange(buffer.Take(result.Count));

            if (!result.EndOfMessage)
            {
                continue;
            }

            string messageText = Encoding.UTF8.GetString(messageBuffer.ToArray());
            
            return messageText;
        }

        return null;
    }

    /// <summary>
    /// Sends a message to the WebSocket
    /// </summary>
    public async Task SendMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken = default)
    {
        if (webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not in Open state");
        }
        
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}
