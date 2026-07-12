namespace HASmartCharge.Backend.Services;

/// <summary>
/// Result of a single EPEX price fetch attempt.
/// </summary>
public record PriceFetchResult(bool Success, int PricesUpserted, bool TomorrowAvailable, string? Error);

/// <summary>
/// Fetches EPEX hourly prices from the configured provider and upserts them into the cache.
/// </summary>
public interface IPriceFetcher
{
    Task<PriceFetchResult> FetchAndStoreAsync(CancellationToken ct = default);
}
