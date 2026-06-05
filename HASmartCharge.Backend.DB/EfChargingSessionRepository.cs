using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HASmartCharge.Backend.DB;

public sealed class EfChargingSessionRepository : IChargingSessionRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfChargingSessionRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ChargingSession?> GetByTransactionIdAsync(int transactionId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ef = await db.ChargingTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        return ef is null ? null : ToDomain(ef);
    }

    public async Task<IReadOnlyList<ChargingSession>> GetActiveSessionsAsync(string? chargePointId = null, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = db.ChargingTransactions.Where(t => t.StopTime == null);
        if (chargePointId is not null)
            query = query.Where(t => t.ChargePointId == chargePointId);
        var results = await query.ToListAsync(ct);
        return results.Select(ToDomain).ToList().AsReadOnly();
    }

    public async Task<int> BeginSessionAsync(ChargingSession session, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ef = new ChargingTransaction
        {
            ChargePointId = session.ChargePointId,
            ConnectorId = session.ConnectorId,
            IdTag = session.IdTag,
            StartTime = session.StartedAt.UtcDateTime,
            MeterStartWh = session.MeterStartWh
        };
        db.ChargingTransactions.Add(ef);
        await db.SaveChangesAsync(ct);
        return ef.Id;
    }

    public async Task SaveAsync(ChargingSession session, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ef = await db.ChargingTransactions.FirstOrDefaultAsync(t => t.Id == session.TransactionId, ct);
        if (ef is null) return;
        if (session.CompletedAt.HasValue)
        {
            ef.StopTime = session.CompletedAt.Value.UtcDateTime;
            ef.MeterStopWh = session.MeterStopWh;
            ef.StopReason = session.StopReason;
        }
        await db.SaveChangesAsync(ct);
    }

    private static ChargingSession ToDomain(ChargingTransaction ef) =>
        ChargingSession.Reconstitute(
            ef.Id,
            ef.ChargePointId,
            ef.ConnectorId,
            ef.IdTag,
            ef.MeterStartWh,
            new DateTimeOffset(ef.StartTime, TimeSpan.Zero),
            ef.MeterStopWh,
            ef.StopReason,
            ef.StopTime.HasValue ? new DateTimeOffset(ef.StopTime.Value, TimeSpan.Zero) : null);
}
