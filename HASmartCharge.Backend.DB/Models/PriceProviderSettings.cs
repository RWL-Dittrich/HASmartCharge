namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// EPEX price provider configuration. Single row (Id = 1).
/// </summary>
public class PriceProviderSettings
{
    public int Id { get; set; }

    /// <summary>Full API URL; templated from <see cref="SupplierSlug"/> by the UI.</summary>
    public string ApiUrl { get; set; } = "https://epexprijzen.nl/api/v1/prices/nextenergy/hourly";

    public string SupplierSlug { get; set; } = "nextenergy";

    public string Currency { get; set; } = "EUR";

    /// <summary>Refresh cadence in minutes; a forced refresh also runs after 13:00 CET for tomorrow's prices.</summary>
    public int RefreshMinutes { get; set; } = 60;
}
