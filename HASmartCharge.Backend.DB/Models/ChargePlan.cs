namespace HASmartCharge.Backend.DB.Models;

public enum ChargePlanStatus
{
    Pending,
    Active,
    Completed,
    Cancelled,
    MissedDeadline
}

/// <summary>
/// A "full by deadline" charge plan. The orchestrator recomputes the
/// cheapest-hour selection each tick and persists it here.
/// </summary>
public class ChargePlan
{
    public int Id { get; set; }

    public DateTime DeadlineUtc { get; set; }

    public int TargetSocPercent { get; set; }

    /// <summary>SoC captured when the plan was created.</summary>
    public int? StartSocPercent { get; set; }

    public ChargePlanStatus Status { get; set; } = ChargePlanStatus.Pending;

    public double EstimatedEnergyKwh { get; set; }

    public decimal EstimatedCost { get; set; }

    /// <summary>JSON array of selected UTC hour starts; recomputed each orchestrator tick.</summary>
    public string SelectedHoursJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
