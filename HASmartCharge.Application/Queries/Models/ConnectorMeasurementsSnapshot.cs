namespace HASmartCharge.Application.Queries.Models;

/// <summary>
/// Immutable read snapshot for the latest connector measurements.
/// </summary>
public sealed record ConnectorMeasurementsSnapshot
{
    public int ConnectorId { get; init; }
    public DateTime LastUpdated { get; init; }

    public MeasurementValueSnapshot? ImportedEnergy { get; init; }
    public MeasurementValueSnapshot? ImportedReactiveEnergy { get; init; }
    public MeasurementValueSnapshot? ExportedEnergy { get; init; }
    public MeasurementValueSnapshot? ExportedReactiveEnergy { get; init; }

    public MeasurementValueSnapshot? ImportedPower { get; init; }
    public MeasurementValueSnapshot? ImportedReactivePower { get; init; }
    public MeasurementValueSnapshot? OfferedPower { get; init; }

    public MeasurementValueSnapshot? VoltageL1 { get; init; }
    public MeasurementValueSnapshot? VoltageL2 { get; init; }
    public MeasurementValueSnapshot? VoltageL3 { get; init; }
    public MeasurementValueSnapshot? VoltageL1N { get; init; }
    public MeasurementValueSnapshot? VoltageL2N { get; init; }
    public MeasurementValueSnapshot? VoltageL3N { get; init; }

    public MeasurementValueSnapshot? ImportedCurrentL1 { get; init; }
    public MeasurementValueSnapshot? ImportedCurrentL2 { get; init; }
    public MeasurementValueSnapshot? ImportedCurrentL3 { get; init; }
    public MeasurementValueSnapshot? ExportedCurrentL1 { get; init; }
    public MeasurementValueSnapshot? ExportedCurrentL2 { get; init; }
    public MeasurementValueSnapshot? ExportedCurrentL3 { get; init; }
    public MeasurementValueSnapshot? OfferedCurrent { get; init; }

    public MeasurementValueSnapshot? Temperature { get; init; }
    public MeasurementValueSnapshot? StateOfCharge { get; init; }
    public MeasurementValueSnapshot? Frequency { get; init; }
    public MeasurementValueSnapshot? RevolutionsPerMinute { get; init; }
}
