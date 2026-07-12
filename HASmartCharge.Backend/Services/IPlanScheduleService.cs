using HASmartCharge.Core.Scheduling;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Result of running the schedule calculator against live CarSettings/ChargerSettings/prices,
/// with the current SoC read (or a warning if it couldn't be read).
/// </summary>
public record PlanCalculation(
    double? SocPercent,
    string? Warning,
    ScheduleResult Schedule);

/// <summary>
/// Builds a <see cref="ScheduleRequest"/> from current settings/prices/SoC and runs the calculator.
/// Shared by the preview and create endpoints on PlanController.
/// </summary>
public interface IPlanScheduleService
{
    Task<PlanCalculation> ComputeAsync(DateTime deadlineUtc, int? targetSocPercent, CancellationToken ct = default);

    /// <summary>
    /// Same as above, but skips the HA SoC fetch and uses <paramref name="knownSocPercent"/> instead —
    /// for callers (the orchestrator) that already read the current SoC this tick.
    /// </summary>
    Task<PlanCalculation> ComputeAsync(DateTime deadlineUtc, int? targetSocPercent, double knownSocPercent, CancellationToken ct = default);
}
