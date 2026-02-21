using HASmartCharge.Backend.OCPP.Models;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Interface for sending commands to charge points
/// </summary>
public interface ICommandSender
{
    Task<bool> SendCommandAsync<TRequest>(string chargePointId, string action, TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation using SessionManager (new architecture)
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
