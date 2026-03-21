using HASmartCharge.Domain.Entities;

namespace HASmartCharge.Application.Interfaces;

/// <summary>Repository for charging session aggregates.</summary>
public interface IChargingSessionRepository
{
    Task<ChargingSession?> GetByTransactionIdAsync(int transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<ChargingSession>> GetActiveSessionsAsync(string? chargePointId = null, CancellationToken ct = default);
    Task<int> BeginSessionAsync(ChargingSession session, CancellationToken ct = default);
    Task SaveAsync(ChargingSession session, CancellationToken ct = default);
}
