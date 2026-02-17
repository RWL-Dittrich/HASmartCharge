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

    public void UnregisterSession(string chargePointId)
    {
        if (_sessionsByChargePointId.TryRemove(chargePointId, out IChargePointSession? session))
        {
            _sessionsByConnectionId.TryRemove(session.Connection.ConnectionId, out _);

            _logger.LogInformation(
                "[{ChargePointId}] Session unregistered (ConnectionId: {ConnectionId})",
                session.ChargePointId,
                session.Connection.ConnectionId);
        }
    }

    public IChargePointSession? GetByChargePointId(string chargePointId)
    {
        _sessionsByChargePointId.TryGetValue(chargePointId, out IChargePointSession? session);
        return session;
    }

    public IChargePointSession? GetByConnectionId(string connectionId)
    {
        _sessionsByConnectionId.TryGetValue(connectionId, out IChargePointSession? session);
        return session;
    }

    public bool IsConnected(string chargePointId)
    {
        return _sessionsByChargePointId.TryGetValue(chargePointId, out IChargePointSession? session) 
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
