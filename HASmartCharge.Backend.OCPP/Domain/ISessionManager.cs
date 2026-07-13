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
    /// Unregister a session. Removes the session's connection mapping and,
    /// only if the charge-point key still points to this exact session, its
    /// charge-point mapping too. Returns true when this session was still the
    /// current registration for its charge point (i.e. not already superseded
    /// by a reconnect).
    /// </summary>
    bool UnregisterSession(IChargePointSession session);
    
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
