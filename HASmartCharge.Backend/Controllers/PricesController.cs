using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Cached EPEX hourly prices, and a manual refresh trigger.
/// </summary>
[ApiController]
[Route("api/prices")]
public class PricesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPriceFetcher _priceFetcher;

    public PricesController(ApplicationDbContext dbContext, IPriceFetcher priceFetcher)
    {
        _dbContext = dbContext;
        _priceFetcher = priceFetcher;
    }

    /// <summary>
    /// Cached hourly prices ordered by hour. Defaults to start of today UTC through +48h.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPrices([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var rangeStart = from ?? DateTime.UtcNow.Date;
        var rangeEnd = to ?? rangeStart.AddHours(48);

        var prices = await _dbContext.HourlyPrices
            .AsNoTracking()
            .Where(p => p.HourStartUtc >= rangeStart && p.HourStartUtc < rangeEnd)
            .OrderBy(p => p.HourStartUtc)
            .ToListAsync(ct);

        // SQLite round-trips DateTime as Kind=Unspecified; restamp as UTC so JSON carries the Z suffix.
        return Ok(prices.Select(p => new
        {
            hourStartUtc = DateTime.SpecifyKind(p.HourStartUtc, DateTimeKind.Utc),
            pricePerKwh = p.PricePerKwh,
            fetchedAt = DateTime.SpecifyKind(p.FetchedAt, DateTimeKind.Utc)
        }));
    }

    /// <summary>
    /// Forces an immediate price fetch. Returns 200 even on failure; check the result body.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var result = await _priceFetcher.FetchAndStoreAsync(ct);
        return Ok(result);
    }
}
