using System.Net.WebSockets;
using System.Text;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Service for handling WebSocket message buffering and communication
/// </summary>
public class WebSocketMessageService
{
    /// <summary>
    /// Receives a complete message from the WebSocket
    /// </summary>
    public async Task<string?> ReceiveMessageAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
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

            var messageText = Encoding.UTF8.GetString(messageBuffer.ToArray());
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

        var messageBytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}
