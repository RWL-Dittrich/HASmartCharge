using System.Collections.Concurrent;
using System.Globalization;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.OCPP.Models;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Core.Costing;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Persists OCPP transactions as <see cref="ChargeSession"/> rows with per-hour cost
/// attribution (see plan.md §6.2 / §7 "Cost attribution").
///
/// Each meter sample is folded into the DB hour buckets incrementally (delta from the last
/// persisted sample), so cost survives a restart mid-session — there is no in-memory sample
/// history to lose. The persisted cursor (<see cref="ChargeSession.LastSampleAtUtc"/> /
/// <see cref="ChargeSession.LastSampleKwh"/>) is where attribution resumes.
///
/// Singleton: DB access happens through a fresh scope per event, since ApplicationDbContext is
/// scoped. Every handler is wrapped in try/catch — a telemetry callback must never throw into
/// the OCPP session. Per-transaction writes are serialized by a <see cref="SemaphoreSlim"/> gate
/// so concurrent samples/finalize don't race the same rows.
/// </summary>
public class ChargeSessionRecorder : IChargerTelemetrySink
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChargeSessionRecorder> _logger;

    // Maps "chargePointId:connectorId" -> the open transaction id on that connector. Meter values
    // arrive per connector WITHOUT a transaction id on some chargers, so samples are resolved by
    // connector; terminal StatusNotifications finalize the right session the same way.
    private readonly ConcurrentDictionary<string, int> _connectorTransactions = new();

    // One write gate per open transaction, so a sample fold can't race the finalize (or another
    // sample) for the same session.
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _txGates = new();

    // Connector states that mean "the transaction is over". SuspendedEV/SuspendedEVSE are
    // deliberately excluded — they're pauses that may resume on the same transaction.
    private static readonly HashSet<string> _terminalConnectorStates =
        new(StringComparer.OrdinalIgnoreCase) { "Finishing", "Available", "Faulted" };

    private static readonly ChargePlanStatus[] _attributablePlanStatuses =
    [
        ChargePlanStatus.Pending, ChargePlanStatus.Active, ChargePlanStatus.MissedDeadline
    ];

    private static string ConnectorKey(string chargePointId, int connectorId) => $"{chargePointId}:{connectorId}";

    public ChargeSessionRecorder(IServiceScopeFactory scopeFactory, ILogger<ChargeSessionRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    #region IChargerTelemetrySink

    public void OnConnected(string chargePointId)
    {
        // No-op: connection lifecycle is ChargerStatusTracker's concern.
    }

    public void OnDisconnected(string chargePointId)
    {
        // No-op.
    }

    public void OnBoot(string chargePointId, ChargerInfo info)
    {
        // No-op.
    }

    public void OnConnectorStatus(string chargePointId, int connectorId, string status, string? errorCode)
    {
        // Some chargers never emit StopTransaction; they signal the end of a transaction by
        // moving the connector to a terminal state. Finalize the open session on that edge.
        // TryRemove makes this fire exactly once even though Finishing is usually followed by
        // Available for the same transaction.
        if (!_terminalConnectorStates.Contains(status))
        {
            return;
        }

        if (_connectorTransactions.TryRemove(ConnectorKey(chargePointId, connectorId), out var transactionId))
        {
            _ = FinalizeSessionAsync(transactionId, DateTimeOffset.UtcNow, meterStopKwhOverride: null, reason: status);
        }
    }

    public void OnTransactionStarted(string chargePointId, int connectorId, int transactionId, int meterStartWh, string? idTag, DateTimeOffset startedAt) =>
        _ = HandleTransactionStartedAsync(chargePointId, connectorId, transactionId, meterStartWh, startedAt);

    public void OnMeterValues(string chargePointId, MeterValuesRequest request)
    {
        try
        {
            // Resolve by connector: MeterValues carry a connectorId but not necessarily a
            // transactionId on this hardware.
            if (!_connectorTransactions.TryGetValue(ConnectorKey(chargePointId, request.ConnectorId), out var transactionId))
            {
                return; // no open transaction on this connector
            }

            foreach (var meterValue in request.MeterValue)
            foreach (var sampledValue in meterValue.SampledValue)
            {
                var measurand = sampledValue.Measurand ?? "Energy.Active.Import.Register";
                if (measurand != "Energy.Active.Import.Register")
                {
                    continue;
                }

                if (!double.TryParse(sampledValue.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
                {
                    continue;
                }

                // OCPP 1.6: a missing unit means Wh for energy measurands.
                var isWh = sampledValue.Unit is null ||
                           sampledValue.Unit.Equals("wh", StringComparison.OrdinalIgnoreCase);
                var kwh = isWh ? raw / 1000.0 : raw;

                _ = FoldSampleAsync(transactionId, AsUtc(meterValue.Timestamp), kwh);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording meter values for {ChargePointId}", chargePointId);
        }
    }

    public void OnTransactionStopped(string chargePointId, int transactionId, int meterStopWh, string? reason, DateTimeOffset stoppedAt) =>
        _ = FinalizeSessionAsync(transactionId, stoppedAt, meterStopWh / 1000.0, reason);

    #endregion

    /// <summary>
    /// Live cost/energy for an in-progress transaction. Because buckets are persisted
    /// incrementally, this just reads the running totals off the session row. Returns null if
    /// the session isn't found or on any error (keeps the status endpoint resilient).
    /// </summary>
    public async Task<CostAttributionResult?> TryGetLiveCostAsync(int transactionId, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var session = await db.ChargeSessions
                .AsNoTracking()
                .Include(s => s.HourlyUsage)
                .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);

            if (session is null)
            {
                return null;
            }

            var hours = session.HourlyUsage
                .OrderBy(u => u.HourStartUtc)
                .Select(u => new HourlyUsageResult(u.HourStartUtc, u.EnergyKwh, u.PricePerKwh, u.Cost))
                .ToList();

            return new CostAttributionResult(session.TotalKwh, session.TotalCost, hours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading live cost for transaction {TransactionId}.", transactionId);
            return null;
        }
    }

    private async Task HandleTransactionStartedAsync(string chargePointId, int connectorId, int transactionId, int meterStartWh, DateTimeOffset startedAt)
    {
        var gate = Gate(transactionId);
        await gate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var startedAtUtc = startedAt.UtcDateTime;

            // A charger that reconnects mid-transaction re-sends StartTransaction for the same
            // physical session (same connector + meter start) and gets a fresh transaction id from
            // the new OCPP session. Those still-open older rows are duplicates — drop them.
            var duplicates = await db.ChargeSessions
                .Include(s => s.HourlyUsage)
                .Where(s => s.TransactionId != transactionId
                            && s.ChargePointId == chargePointId
                            && s.ConnectorId == connectorId
                            && s.MeterStartWh == meterStartWh
                            && s.CompletedAt == null)
                .ToListAsync();

            foreach (var duplicate in duplicates)
            {
                _logger.LogWarning(
                    "Removing duplicate open ChargeSession {DuplicateTransactionId} (same connector {ConnectorId} + meter start {MeterStartWh}Wh as new transaction {TransactionId}).",
                    duplicate.TransactionId, connectorId, meterStartWh, transactionId);
                db.HourlyEnergyUsage.RemoveRange(duplicate.HourlyUsage);
                db.ChargeSessions.Remove(duplicate);
                CleanupTracking(duplicate.TransactionId);
            }

            var existing = await db.ChargeSessions
                .Include(s => s.HourlyUsage)
                .FirstOrDefaultAsync(s => s.TransactionId == transactionId);

            ChargeSession session;
            if (existing is not null)
            {
                _logger.LogWarning(
                    "ChargeSession {TransactionId} already exists; charger reused the transaction id, overwriting.",
                    transactionId);
                db.HourlyEnergyUsage.RemoveRange(existing.HourlyUsage);
                existing.HourlyUsage.Clear();
                session = existing;
            }
            else
            {
                session = new ChargeSession { TransactionId = transactionId };
                db.ChargeSessions.Add(session);
            }

            session.ChargePointId = chargePointId;
            session.ConnectorId = connectorId;
            session.StartedAt = startedAtUtc;
            session.MeterStartWh = meterStartWh;
            session.CompletedAt = null;
            session.MeterStopWh = null;
            session.TotalKwh = 0;
            session.TotalCost = 0;
            // Seed the attribution cursor at the start reading.
            session.LastSampleAtUtc = startedAtUtc;
            session.LastSampleKwh = meterStartWh / 1000.0;

            var activePlan = await db.ChargePlans
                .Where(p => _attributablePlanStatuses.Contains(p.Status))
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
            session.PlanId = activePlan?.Id;

            await db.SaveChangesAsync();

            _connectorTransactions[ConnectorKey(chargePointId, connectorId)] = transactionId;

            _logger.LogInformation(
                "Charge session {TransactionId} started on {ChargePointId} connector {ConnectorId} (meterStart={MeterStartWh}Wh).",
                transactionId, chargePointId, connectorId, meterStartWh);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording transaction start {TransactionId} on {ChargePointId}.", transactionId, chargePointId);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Folds one meter sample into the persisted hour buckets: attribute the delta since the
    /// session's last cursor across the clock-hours it spans, then advance the cursor. Skips
    /// out-of-order/duplicate samples and already-finalized sessions.
    /// </summary>
    private async Task FoldSampleAsync(int transactionId, DateTime sampleAtUtc, double cumulativeKwh)
    {
        var gate = Gate(transactionId);
        await gate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var session = await db.ChargeSessions
                .Include(s => s.HourlyUsage)
                .FirstOrDefaultAsync(s => s.TransactionId == transactionId);

            if (session is null || session.CompletedAt is not null)
            {
                return;
            }

            var from = new MeterSample(session.LastSampleAtUtc ?? session.StartedAt, session.LastSampleKwh ?? session.MeterStartWh / 1000.0);
            if (sampleAtUtc <= from.TimestampUtc)
            {
                return; // out of order or duplicate — nothing to add
            }

            await ApplyIntervalAsync(db, session, from, new MeterSample(sampleAtUtc, cumulativeKwh));

            session.LastSampleAtUtc = sampleAtUtc;
            session.LastSampleKwh = cumulativeKwh;
            RecomputeTotals(session);

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error folding meter sample into transaction {TransactionId}.", transactionId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task FinalizeSessionAsync(int transactionId, DateTimeOffset stoppedAt, double? meterStopKwhOverride, string? reason)
    {
        var gate = Gate(transactionId);
        await gate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var session = await db.ChargeSessions
                .Include(s => s.HourlyUsage)
                .FirstOrDefaultAsync(s => s.TransactionId == transactionId);

            if (session is null)
            {
                _logger.LogWarning("ChargeSession {TransactionId} not found on stop; nothing to finalize.", transactionId);
                return;
            }

            // Idempotent: the terminal-status path can be reached twice (Finishing then Available),
            // and a real StopTransaction may also arrive for an already-finalized session.
            if (session.CompletedAt is not null)
            {
                return;
            }

            var stoppedAtUtc = stoppedAt.UtcDateTime;

            if (meterStopKwhOverride is { } stopKwh)
            {
                // A real StopTransaction gives an authoritative final reading — fold the last
                // interval up to it.
                var from = new MeterSample(session.LastSampleAtUtc ?? session.StartedAt, session.LastSampleKwh ?? session.MeterStartWh / 1000.0);
                await ApplyIntervalAsync(db, session, from, new MeterSample(stoppedAtUtc, stopKwh));
                session.MeterStopWh = (int)Math.Round(stopKwh * 1000.0);
                session.LastSampleAtUtc = stoppedAtUtc;
                session.LastSampleKwh = stopKwh;
            }
            else
            {
                // No StopTransaction reading (terminal-status path): the last folded sample is the
                // stop reading. Buckets are already up to date.
                session.MeterStopWh = session.LastSampleKwh is { } k
                    ? (int)Math.Round(k * 1000.0)
                    : session.MeterStartWh;
            }

            session.CompletedAt = stoppedAtUtc;
            RecomputeTotals(session);

            await db.SaveChangesAsync();

            _logger.LogInformation(
                "Charge session {TransactionId} finalized: {TotalKwh:F3} kWh, {TotalCost:F2} cost, reason={Reason}.",
                transactionId, session.TotalKwh, session.TotalCost, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing charge session {TransactionId}.", transactionId);
        }
        finally
        {
            CleanupTracking(transactionId);
            gate.Release();
        }
    }

    /// <summary>
    /// Attribute the energy delivered between two samples across the clock-hours it spans and
    /// add it to the session's (tracked) hour buckets, pricing each from HourlyPrices.
    /// </summary>
    private static async Task ApplyIntervalAsync(ApplicationDbContext db, ChargeSession session, MeterSample from, MeterSample to)
    {
        var buckets = CostAttributor.AttributeInterval(from, to);
        if (buckets.Count == 0)
        {
            return;
        }

        var hourStarts = buckets.Select(b => b.HourStartUtc).ToList();
        var prices = await db.HourlyPrices
            .Where(p => hourStarts.Contains(p.HourStartUtc))
            .ToDictionaryAsync(p => p.HourStartUtc, p => p.PricePerKwh);

        foreach (var (hourStart, kwh) in buckets)
        {
            var row = session.HourlyUsage.FirstOrDefault(u => u.HourStartUtc == hourStart);
            if (row is null)
            {
                row = new HourlyEnergyUsage { SessionId = session.TransactionId, HourStartUtc = hourStart };
                session.HourlyUsage.Add(row);
            }

            row.EnergyKwh += kwh;
            row.PricePerKwh = prices.GetValueOrDefault(hourStart, 0m); // no price row -> 0-price bucket
            row.Cost = (decimal)row.EnergyKwh * row.PricePerKwh;
        }
    }

    private static void RecomputeTotals(ChargeSession session)
    {
        session.TotalKwh = session.HourlyUsage.Sum(u => u.EnergyKwh);
        session.TotalCost = session.HourlyUsage.Sum(u => u.Cost);
    }

    private SemaphoreSlim Gate(int transactionId) => _txGates.GetOrAdd(transactionId, _ => new SemaphoreSlim(1, 1));

    /// <summary>Drops in-memory tracking for a finished transaction (connector map + write gate).</summary>
    private void CleanupTracking(int transactionId)
    {
        foreach (var entry in _connectorTransactions)
        {
            if (entry.Value == transactionId)
            {
                _connectorTransactions.TryRemove(entry.Key, out _);
            }
        }

        _txGates.TryRemove(transactionId, out _);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
