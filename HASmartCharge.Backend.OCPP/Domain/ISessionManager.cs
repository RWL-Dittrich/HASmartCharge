namespace HASmartCharge.Backend.OCPP.Domain;

/// <summary>
/// Manages active charge point sessions and their lifecycle
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Register a new session
    /// </summary>
    void RegisterSession(IChargePointSession session);
    
    /// <summary>
    /// Unregister a session
    /// </summary>
    void UnregisterSession(string chargePointId);
    
    /// <summary>
    /// Get a session by charge point ID
    /// </summary>
    IChargePointSession? GetByChargePointId(string chargePointId);
    
    /// <summary>
    /// Get a session by connection ID
    /// </summary>
    IChargePointSession? GetByConnectionId(string connectionId);
    
    /// <summary>
    /// Check if a charge point is connected
    /// </summary>
    bool IsConnected(string chargePointId);
    
    /// <summary>
    /// Get all connected charge point IDs
    /// </summary>
    IEnumerable<string> GetConnectedChargePointIds();
    
    /// <summary>
    /// Get all active sessions
    /// </summary>
    IEnumerable<IChargePointSession> GetAllSessions();
}
