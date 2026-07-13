using System.Text.Json;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Transport;

namespace HASmartCharge.Backend.OCPP.Domain;

/// <summary>
/// Represents a connected charge point session
/// Owns charge point state, configuration, and OCPP message handling
/// </summary>
public interface IChargePointSession
{
    /// <summary>
    /// Unique identifier for the charge point (from URL path)
    /// </summary>
    string ChargePointId { get; }
    
    /// <summary>
    /// Connection for this session
    /// </summary>
    IConnection Connection { get; }
    
    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// When the session was established
    /// </summary>
    DateTime ConnectedAt { get; }
    
    /// <summary>
    /// Handle an incoming CALL message and return a response
    /// </summary>
    Task<object> HandleCallAsync(string action, JsonElement payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handle an incoming CALLRESULT message (response to our request)
    /// </summary>
    Task HandleCallResultAsync(string messageId, JsonElement payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handle an incoming CALLERROR message (error response to our request)
    /// </summary>
    Task HandleCallErrorAsync(string messageId, string errorCode, JsonElement payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send a command to the charge point (CSMS-initiated) and await the charger's response.
    /// </summary>
    Task<OcppCommandResult> SendCommandAsync<TRequest>(string action, TRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set availability of a connector (Operative / Inoperative)
    /// </summary>
    Task<OcppCommandResult> SetAvailabilityAsync(int connectorId, bool available, CancellationToken cancellationToken = default);

    /// <summary>
    /// Change configuration on the charge point
    /// </summary>
    Task<OcppCommandResult> ChangeConfigurationAsync(string key, string value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initialize the session (apply initial configuration, etc.)
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleanup the session. Pass <paramref name="notifyDisconnected"/> = false when a
    /// newer session has already superseded this one (reconnect) so a stale teardown
    /// doesn't flip live status to disconnected.
    /// </summary>
    Task DisposeAsync(bool notifyDisconnected = true);
}
