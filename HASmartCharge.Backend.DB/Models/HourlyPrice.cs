namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Cached EPEX hourly price. PK = the UTC hour start.
/// </summary>
public class HourlyPrice
{
    public DateTime HourStartUtc { get; set; }

    /// <summary>All-in €/kWh (supplier price incl. tax/markup).</summary>
    public decimal PricePerKwh { get; set; }

    public DateTime FetchedAt { get; set; }
}
