namespace HASmartCharge.Backend.OCPP.Services;

/// <summary>
/// Values pushed to a charger on connect (and used in the BootNotification response).
/// </summary>
public record OcppChargerConfiguration(
    int HeartbeatIntervalSeconds,
    int MeterValueSampleIntervalSeconds,
    int ClockAlignedDataIntervalSeconds,
    string MeterValuesSampledData)
{
    public static OcppChargerConfiguration Default { get; } = new(
        HeartbeatIntervalSeconds: 60,
        MeterValueSampleIntervalSeconds: 10,
        ClockAlignedDataIntervalSeconds: 10,
        MeterValuesSampledData:
        "Power.Active.Import,Energy.Active.Import.Register,Current.Import,Voltage,Current.Offered,Power.Offered,SoC,Voltage.L1,Voltage.L2,Voltage.L3");
}

/// <summary>
/// Supplies the on-connect configuration for a charge point.
/// Implemented outside Backend.OCPP (settings live in the database).
/// </summary>
public interface IOcppChargerConfigurationProvider
{
    Task<OcppChargerConfiguration> GetConfigurationAsync(string chargePointId, CancellationToken cancellationToken = default);
}
