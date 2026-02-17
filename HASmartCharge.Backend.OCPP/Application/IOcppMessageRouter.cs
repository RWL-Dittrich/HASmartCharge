using HASmartCharge.Backend.OCPP.Transport;

namespace HASmartCharge.Backend.OCPP.Application;

/// <summary>
/// Routes OCPP messages from connections to appropriate handlers
/// Handles message decoding, routing, and response encoding
/// </summary>
public interface IOcppMessageRouter
{
    /// <summary>
    /// Route an incoming raw OCPP message to the appropriate handler
    /// Returns the response message to send back, or null if no response needed
    /// </summary>
    Task<string?> RouteAsync(IConnection connection, string rawMessage, CancellationToken cancellationToken = default);
}
