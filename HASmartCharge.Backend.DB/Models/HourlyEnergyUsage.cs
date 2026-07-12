namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// Per-session per-clock-hour energy + cost breakdown (the cost attribution output).
/// </summary>
public class HourlyEnergyUsage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ChargeSession? Session { get; set; }

    public DateTime HourStartUtc { get; set; }

    public double EnergyKwh { get; set; }

    public decimal PricePerKwh { get; set; }

    public decimal Cost { get; set; }
}
