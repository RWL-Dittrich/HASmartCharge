using HASmartCharge.Backend.DB;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Periodically fetches EPEX hourly prices and keeps the HourlyPrice cache warm.
/// Runs once shortly after startup, then on the configured RefreshMinutes cadence.
/// If tomorrow's prices haven't been published yet, also wakes shortly after 13:00
/// Europe/Amsterdam time (when the provider typically publishes them) if that's sooner
/// than the next regular refresh.
/// </summary>
public class PriceFetchService : BackgroundService
{
    private static readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _minRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _tomorrowPricesPublishTime = new(13, 5, 0);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PriceFetchService> _logger;
    private readonly TimeZoneInfo _amsterdamTimeZone;

    public PriceFetchService(IServiceProvider serviceProvider, ILogger<PriceFetchService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _amsterdamTimeZone = ResolveAmsterdamTimeZone(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price fetch service started.");

        try
        {
            // Let startup migrations settle before hitting the DB.
            await Task.Delay(_startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _minRefreshInterval;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fetcher = scope.ServiceProvider.GetRequiredService<IPriceFetcher>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var result = await fetcher.FetchAndStoreAsync(stoppingToken);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Price fetch tick succeeded: {Count} hours upserted, tomorrow available: {TomorrowAvailable}",
                        result.PricesUpserted, result.TomorrowAvailable);
                }
                else
                {
                    _logger.LogWarning("Price fetch tick failed: {Error}", result.Error);
                }

                var settings = await dbContext.PriceProviderSettings.AsNoTracking().FirstOrDefaultAsync(stoppingToken);
                var refreshInterval = TimeSpan.FromMinutes(settings?.RefreshMinutes ?? 60);
                if (refreshInterval < _minRefreshInterval)
                {
                    refreshInterval = _minRefreshInterval;
                }

                delay = refreshInterval;

                if (!result.TomorrowAvailable)
                {
                    var untilPublish = TimeUntilTomorrowPricesPublish();
                    if (untilPublish is { } wait && wait < delay)
                    {
                        delay = wait;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in price fetch loop.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Price fetch service stopped.");
    }

    /// <summary>
    /// Time remaining until ~13:05 Europe/Amsterdam time today, or null if that time has already passed.
    /// </summary>
    private TimeSpan? TimeUntilTomorrowPricesPublish()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _amsterdamTimeZone);
        var publishToday = nowLocal.Date + _tomorrowPricesPublishTime;

        return nowLocal < publishToday ? publishToday - nowLocal : null;
    }

    private static TimeZoneInfo ResolveAmsterdamTimeZone(ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning("Time zone 'Europe/Amsterdam' not found; falling back to 'W. Europe Standard Time'.");
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
