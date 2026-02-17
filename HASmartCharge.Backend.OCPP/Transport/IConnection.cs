namespace HASmartCharge.Backend.OCPP.Transport;

/// <summary>
/// Abstraction for a transport connection (WebSocket, TCP, etc.)
/// Isolates domain logic from transport implementation details
/// </summary>
public interface IConnection
{
    /// <summary>
    /// Unique identifier for this connection
    /// </summary>
    string ConnectionId { get; }
    
    /// <summary>
    /// Remote endpoint information (IP address, port, etc.)
    /// </summary>
    string RemoteEndPoint { get; }
    
    /// <summary>
    /// Whether the connection is currently open
    /// </summary>
    bool IsOpen { get; }
    
    /// <summary>
    /// Send a message through the connection
    /// </summary>
    Task SendAsync(string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Close the connection
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
