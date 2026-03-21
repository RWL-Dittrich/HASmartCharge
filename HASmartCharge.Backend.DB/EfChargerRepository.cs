using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Connector = HASmartCharge.Domain.Entities.Connector;

namespace HASmartCharge.Backend.DB;

public sealed class EfChargerRepository : IChargerRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfChargerRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Domain.Entities.Charger?> GetByIdAsync(string chargePointId, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Charger? efCharger = await db.Chargers
            .Include(c => c.Connectors)
            .FirstOrDefaultAsync(c => c.ChargePointId == chargePointId, ct);
        return efCharger is null ? null : ToDomain(efCharger);
    }

    public async Task<IReadOnlyList<Domain.Entities.Charger>> GetAllAsync(CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<Charger> efChargers = await db.Chargers
            .Include(c => c.Connectors)
            .ToListAsync(ct);
        return efChargers.Select(ToDomain).ToList().AsReadOnly();
    }

    public async Task SaveAsync(Domain.Entities.Charger charger, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Charger? efCharger = await db.Chargers
            .Include(c => c.Connectors)
            .FirstOrDefaultAsync(c => c.ChargePointId == charger.ChargePointId, ct);

        if (efCharger is null)
        {
            efCharger = new Models.Charger
            {
                ChargePointId = charger.ChargePointId,
                FirstSeenAt = charger.RegisteredAt.UtcDateTime,
                LastConnectedAt = charger.LastConnectedAt.HasValue
                    ? charger.LastConnectedAt.Value.UtcDateTime
                    : DateTime.UtcNow
            };
            db.Chargers.Add(efCharger);
        }

        efCharger.Vendor = charger.Vendor;
        efCharger.Model = charger.Model;
        efCharger.SerialNumber = charger.SerialNumber;
        efCharger.FirmwareVersion = charger.FirmwareVersion;
        if (charger.LastConnectedAt.HasValue)
            efCharger.LastConnectedAt = charger.LastConnectedAt.Value.UtcDateTime;

        // Sync connectors
        foreach (Connector connector in charger.Connectors)
        {
            Models.Connector? efConnector = efCharger.Connectors
                .FirstOrDefault(c => c.ConnectorId == connector.ConnectorId);
            if (efConnector is null)
            {
                efConnector = new Models.Connector
                {
                    ChargePointId = charger.ChargePointId,
                    ConnectorId = connector.ConnectorId,
                    FirstSeenAt = DateTime.UtcNow
                };
                efCharger.Connectors.Add(efConnector);
            }
            efConnector.LastStatus = connector.Status;
            efConnector.LastErrorCode = connector.ErrorCode;
            efConnector.LastStatusUpdateAt = connector.LastStatusUpdatedAt.UtcDateTime;
        }

        await db.SaveChangesAsync(ct);
    }

    private static Domain.Entities.Charger ToDomain(Models.Charger ef) =>
        Domain.Entities.Charger.Reconstitute(
            ef.ChargePointId,
            ef.Vendor ?? "",
            ef.Model ?? "",
            ef.SerialNumber,
            ef.FirmwareVersion,
            isConnected: false,      // connection state is in-memory only
            lastConnectedAt: new DateTimeOffset(ef.LastConnectedAt, TimeSpan.Zero),
            lastDisconnectedAt: null,
            registeredAt: new DateTimeOffset(ef.FirstSeenAt, TimeSpan.Zero),
            connectors: ef.Connectors.Select(c => (c.ConnectorId, c.LastStatus ?? "Unknown", c.LastErrorCode)));
}
