using System.Text.Json;
using System.Text.Json.Serialization;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Fetches today/tomorrow hourly prices from the EPEX provider API and upserts them into
/// the HourlyPrice cache. Scoped (uses the request-scoped ApplicationDbContext).
/// </summary>
public class EpexPriceFetcher : IPriceFetcher
{
    // The provider returns 403 without a browser User-Agent.
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EpexPriceFetcher> _logger;

    public EpexPriceFetcher(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<EpexPriceFetcher> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PriceFetchResult> FetchAndStoreAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _dbContext.PriceProviderSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (settings is null)
            {
                const string error = "No PriceProviderSettings row found.";
                _logger.LogWarning("Price fetch skipped: {Error}", error);
                return new PriceFetchResult(false, 0, false, error);
            }

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            using var response = await client.GetAsync(settings.ApiUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = $"EPEX API returned {(int)response.StatusCode} {response.ReasonPhrase}";
                _logger.LogWarning("Price fetch failed: {Error}", error);
                return new PriceFetchResult(false, 0, false, error);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<EpexResponse>(body, _jsonOptions);
            if (parsed is null)
            {
                const string error = "Empty or unparseable EPEX response body.";
                _logger.LogWarning("Price fetch failed: {Error}", error);
                return new PriceFetchResult(false, 0, false, error);
            }

            var today = parsed.Today ?? [];
            var tomorrow = parsed.Tomorrow ?? [];
            var allPoints = today.Concat(tomorrow).ToList();

            var hours = allPoints.Select(p => DateTime.SpecifyKind(p.T, DateTimeKind.Utc)).ToList();
            var existingRows = await _dbContext.HourlyPrices
                .Where(p => hours.Contains(p.HourStartUtc))
                .ToDictionaryAsync(p => p.HourStartUtc, ct);

            var fetchedAt = DateTime.UtcNow;
            var upserted = 0;

            foreach (var point in allPoints)
            {
                var hourStartUtc = DateTime.SpecifyKind(point.T, DateTimeKind.Utc);

                if (existingRows.TryGetValue(hourStartUtc, out var existing))
                {
                    existing.PricePerKwh = point.Price;
                    existing.FetchedAt = fetchedAt;
                }
                else
                {
                    _dbContext.HourlyPrices.Add(new HourlyPrice
                    {
                        HourStartUtc = hourStartUtc,
                        PricePerKwh = point.Price,
                        FetchedAt = fetchedAt
                    });
                }

                upserted++;
            }

            await _dbContext.SaveChangesAsync(ct);

            var tomorrowAvailable = tomorrow.Count > 0;
            _logger.LogInformation(
                "Price fetch succeeded: {Count} hours upserted, tomorrow available: {TomorrowAvailable}",
                upserted, tomorrowAvailable);

            return new PriceFetchResult(true, upserted, tomorrowAvailable, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Price fetch failed.");
            return new PriceFetchResult(false, 0, false, ex.Message);
        }
    }

    private class EpexResponse
    {
        [JsonPropertyName("today")]
        public List<EpexPricePoint>? Today { get; set; }

        [JsonPropertyName("tomorrow")]
        public List<EpexPricePoint>? Tomorrow { get; set; }
    }

    private class EpexPricePoint
    {
        [JsonPropertyName("t")]
        public DateTime T { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }
}
