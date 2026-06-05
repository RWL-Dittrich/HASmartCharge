using HASmartCharge.Application.Interfaces;
using HASmartCharge.Domain.Entities;

namespace HASmartCharge.Application.Commands;

public sealed class BeginChargingSessionHandler
{
    private readonly IChargingSessionRepository _sessions;

    public BeginChargingSessionHandler(IChargingSessionRepository sessions)
    {
        _sessions = sessions;
    }

    /// <summary>Begins a new charging session and returns the assigned transaction ID.</summary>
    public async Task<int> HandleAsync(BeginChargingSessionCommand command, CancellationToken ct = default)
    {
        var session = ChargingSession.Begin(0, command.ChargePointId, command.ConnectorId, command.IdTag, command.MeterStartWh, command.StartedAt);
        return await _sessions.BeginSessionAsync(session, ct);
    }
}
