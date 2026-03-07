using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HASmartCharge.Backend.DB;

/// <summary>
/// EF Core implementation of IOcppPersistence.
/// Uses IServiceScopeFactory to create short-lived scopes per operation,
/// since consumers (ChargePointSession) are long-lived per-connection objects.
/// </summary>
public class OcppRepository : IOcppPersistence
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcppRepository> _logger;

    public OcppRepository(IServiceScopeFactory scopeFactory, ILogger<OcppRepository> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task UpsertChargerAsync(string chargePointId, OcppBootInfo? bootInfo, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Charger? charger = await db.Chargers.FindAsync([chargePointId], ct);

        if (charger is null)
        {
            charger = new Charger
            {
                ChargePointId = chargePointId,
                FirstSeenAt = DateTime.UtcNow,
                LastConnectedAt = DateTime.UtcNow
            };
            db.Chargers.Add(charger);
        }
        else
        {
            charger.LastConnectedAt = DateTime.UtcNow;
        }

        if (bootInfo is not null)
        {
            charger.Vendor = bootInfo.Vendor;
            charger.Model = bootInfo.Model;
            charger.SerialNumber = bootInfo.SerialNumber;
            charger.FirmwareVersion = bootInfo.FirmwareVersion;
            charger.LastBootNotificationAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Upserted charger {ChargePointId} (boot={HasBoot})",
            chargePointId, bootInfo is not null);
    }

    public async Task UpsertConnectorAsync(string chargePointId, int connectorId, string? status, string? errorCode, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure the charger exists first
        Charger? charger = await db.Chargers.FindAsync([chargePointId], ct);
        if (charger is null)
        {
            charger = new Charger
            {
                ChargePointId = chargePointId,
                FirstSeenAt = DateTime.UtcNow,
                LastConnectedAt = DateTime.UtcNow
            };
            db.Chargers.Add(charger);
        }

        Connector? connector = await db.Connectors
            .FirstOrDefaultAsync(c => c.ChargePointId == chargePointId && c.ConnectorId == connectorId, ct);

        if (connector is null)
        {
            connector = new Connector
            {
                ChargePointId = chargePointId,
                ConnectorId = connectorId,
                FirstSeenAt = DateTime.UtcNow
            };
            db.Connectors.Add(connector);
        }

        connector.LastStatus = status;
        connector.LastErrorCode = errorCode;
        connector.LastStatusUpdateAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Upserted connector {ChargePointId}/{ConnectorId} status={Status}",
            chargePointId, connectorId, status);
    }

    public async Task<int> BeginTransactionAsync(string chargePointId, int connectorId, string idTag, DateTime startTime, int meterStartWh, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChargingTransaction transaction = new ChargingTransaction
        {
            ChargePointId = chargePointId,
            ConnectorId = connectorId,
            IdTag = idTag,
            StartTime = startTime,
            MeterStartWh = meterStartWh
        };

        db.ChargingTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Created transaction {TransactionId} for {ChargePointId}/{ConnectorId} idTag={IdTag}",
            transaction.Id, chargePointId, connectorId, idTag);

        return transaction.Id;
    }

    public async Task CompleteTransactionAsync(int transactionId, DateTime stopTime, int meterStopWh, string? reason, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChargingTransaction? transaction = await db.ChargingTransactions.FindAsync([transactionId], ct);

        if (transaction is null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for completion", transactionId);
            return;
        }

        transaction.StopTime = stopTime;
        transaction.MeterStopWh = meterStopWh;
        transaction.StopReason = reason;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Completed transaction {TransactionId}: stop={StopTime}, meterStop={MeterStopWh}Wh, reason={Reason}",
            transactionId, stopTime, meterStopWh, reason);
    }

    public async Task<List<PersistedCharger>> GetAllChargersAsync(CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<PersistedCharger> chargers = await db.Chargers
            .Include(c => c.Connectors)
            .Select(c => new PersistedCharger
            {
                ChargePointId = c.ChargePointId,
                Vendor = c.Vendor,
                Model = c.Model,
                SerialNumber = c.SerialNumber,
                FirmwareVersion = c.FirmwareVersion,
                FirstSeenAt = c.FirstSeenAt,
                LastConnectedAt = c.LastConnectedAt,
                Connectors = c.Connectors.Select(conn => new PersistedConnector
                {
                    ConnectorId = conn.ConnectorId,
                    LastStatus = conn.LastStatus,
                    LastErrorCode = conn.LastErrorCode
                }).ToList()
            })
            .ToListAsync(ct);

        _logger.LogInformation("Loaded {Count} chargers from database for seeding", chargers.Count);

        return chargers;
    }
}


