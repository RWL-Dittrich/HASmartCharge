using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Domain;

/// <summary>
/// Manages active charge point sessions
/// Tracks session mappings by both charge point ID and connection ID
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly ConcurrentDictionary<string, IChargePointSession> _sessionsByChargePointId = new();
    private readonly ConcurrentDictionary<string, IChargePointSession> _sessionsByConnectionId = new();

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterSession(IChargePointSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        _sessionsByChargePointId[session.ChargePointId] = session;
        _sessionsByConnectionId[session.Connection.ConnectionId] = session;

        _logger.LogInformation(
            "[{ChargePointId}] Session registered (ConnectionId: {ConnectionId})",
            session.ChargePointId,
            session.Connection.ConnectionId);
    }

    public bool UnregisterSession(IChargePointSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        // Always drop this session's own connection mapping.
        _sessionsByConnectionId.TryRemove(session.Connection.ConnectionId, out _);

        // Only drop the charge-point mapping if it STILL points to this exact session.
        // On a reconnect a newer session may already own the chargePointId key; a stale
        // session's teardown must not clobber it. TryRemove(KeyValuePair) is atomic on
        // both key and value (reference equality for the session).
        var wasCurrent = _sessionsByChargePointId.TryRemove(
            new KeyValuePair<string, IChargePointSession>(session.ChargePointId, session));

        _logger.LogInformation(
            "[{ChargePointId}] Session unregistered (ConnectionId: {ConnectionId}, WasCurrent: {WasCurrent})",
            session.ChargePointId,
            session.Connection.ConnectionId,
            wasCurrent);

        return wasCurrent;
    }

    public IChargePointSession? GetByChargePointId(string chargePointId)
    {
        _sessionsByChargePointId.TryGetValue(chargePointId, out var session);
        return session;
    }

    public IChargePointSession? GetByConnectionId(string connectionId)
    {
        _sessionsByConnectionId.TryGetValue(connectionId, out var session);
        return session;
    }

    public bool IsConnected(string chargePointId)
    {
        return _sessionsByChargePointId.TryGetValue(chargePointId, out var session) 
               && session.IsActive 
               && session.Connection.IsOpen;
    }

    public IEnumerable<string> GetConnectedChargePointIds()
    {
        return _sessionsByChargePointId
            .Where(kvp => kvp.Value.IsActive && kvp.Value.Connection.IsOpen)
            .Select(kvp => kvp.Key);
    }

    public IEnumerable<IChargePointSession> GetAllSessions()
    {
        return _sessionsByChargePointId.Values;
    }
}
