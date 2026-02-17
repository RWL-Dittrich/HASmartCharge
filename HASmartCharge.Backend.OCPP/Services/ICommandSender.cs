using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Interface for sending commands to charge points
/// Abstracts the underlying implementation (old or new architecture)
/// </summary>
public interface ICommandSender
{
    Task<bool> SendCommandAsync<TRequest>(string chargePointId, string action, TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Legacy implementation using ChargerConnectionManager
/// </summary>
public class LegacyCommandSender : ICommandSender
{
    private readonly ChargerConnectionManager _connectionManager;

    public LegacyCommandSender(ChargerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async Task<bool> SendCommandAsync<TRequest>(
        string chargePointId,
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _connectionManager.SendCommandAsync(chargePointId, action, request, cancellationToken);
    }
}

/// <summary>
/// New implementation using SessionManager
/// </summary>
public class SessionCommandSender : ICommandSender
{
    private readonly Domain.ISessionManager _sessionManager;

    public SessionCommandSender(Domain.ISessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public async Task<bool> SendCommandAsync<TRequest>(
        string chargePointId,
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = _sessionManager.GetByChargePointId(chargePointId);
        if (session == null)
        {
            return false;
        }

        return await session.SendCommandAsync(action, request, cancellationToken);
    }
}
