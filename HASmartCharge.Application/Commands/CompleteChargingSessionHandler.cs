using HASmartCharge.Application.Interfaces;

namespace HASmartCharge.Application.Commands;

public sealed class CompleteChargingSessionHandler
{
    private readonly IChargingSessionRepository _sessions;

    public CompleteChargingSessionHandler(IChargingSessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task HandleAsync(CompleteChargingSessionCommand command, CancellationToken ct = default)
    {
        var session = await _sessions.GetByTransactionIdAsync(command.TransactionId, ct);
        if (session is null) return;
        session.Complete(command.MeterStopWh, command.StopReason, command.CompletedAt);
        await _sessions.SaveAsync(session, ct);
    }
}
