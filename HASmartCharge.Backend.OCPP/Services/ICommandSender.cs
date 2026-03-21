using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Models;

namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Interface for sending commands to charge points
/// </summary>
public interface ICommandSender
{
    Task<OcppCommandResult> SendCommandAsync<TRequest>(string chargePointId, string action, TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation using SessionManager (new architecture)
/// </summary>
public class SessionCommandSender : ICommandSender
{
    private readonly ISessionManager _sessionManager;

    public SessionCommandSender(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public async Task<OcppCommandResult> SendCommandAsync<TRequest>(
        string chargePointId,
        string action,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        IChargePointSession? session = _sessionManager.GetByChargePointId(chargePointId);
        if (session == null)
        {
            return OcppCommandResult.FromCallError("NotConnected", $"Charge point '{chargePointId}' is not connected");
        }

        return await session.SendCommandAsync(action, request, cancellationToken);
    }
}
