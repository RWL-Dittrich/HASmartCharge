using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Supplies on-connect charger configuration from the ChargerSettings table.
/// Singleton (consumed by the singleton OCPP stack), so it opens a scope per call.
/// </summary>
public class DbOcppChargerConfigurationProvider : IOcppChargerConfigurationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbOcppChargerConfigurationProvider> _logger;

    public DbOcppChargerConfigurationProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<DbOcppChargerConfigurationProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<OcppChargerConfiguration> GetConfigurationAsync(string chargePointId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Single-charger setup: one settings row (Id = 1) applies to whatever connects.
            var settings = await dbContext.ChargerSettings.AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (settings is null)
            {
                _logger.LogWarning("No ChargerSettings row found; using default OCPP configuration for {ChargePointId}", chargePointId);
                return OcppChargerConfiguration.Default;
            }

            return new OcppChargerConfiguration(
                settings.HeartbeatInterval,
                settings.MeterValueSampleInterval,
                settings.ClockAlignedDataInterval,
                settings.MeterValuesSampledData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ChargerSettings; using default OCPP configuration for {ChargePointId}", chargePointId);
            return OcppChargerConfiguration.Default;
        }
    }
}
