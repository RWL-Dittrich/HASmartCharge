using System.Net.WebSockets;
using System.Text;

namespace HASmartCharge.Backend.Services.Ocpp;

public class OcppWebSocketHandler
{
    public delegate Task ReceiveMessageHandler(byte[] data);

    private WebSocket? _webSocket;

    public event ReceiveMessageHandler? OnReceiveMessage;

    public void Start(WebSocket webSocket)
    {
        _webSocket = webSocket;
        _ = Task.Run(ReceiveTask);
    }

    private async Task ReceiveTask()
    {
        if (_webSocket == null)
        {
            throw new InvalidOperationException("WebSocket not initialized");
        }

        var msgBuffer = new List<byte>();

        var buffer = new byte[4096];

        while (_webSocket.State == WebSocketState.Open)
        {
            Array.Clear(buffer);

            var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            else
            {
                msgBuffer.AddRange(buffer.Take(result.Count));

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (OnReceiveMessage != null)
                {
                    await OnReceiveMessage(msgBuffer.ToArray());
                }

                msgBuffer.Clear();
            }
        }
    }

    public async Task Send(string data)
    {
        if (_webSocket == null)
        {
            throw new InvalidOperationException("WebSocket not initialized");
        }

        await _webSocket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
