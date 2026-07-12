namespace HASmartCharge.Backend.DB.Models;

/// <summary>
/// The one OCPP charger. Single row (Id = 1); schema allows more, UI assumes one.
/// </summary>
public class ChargerSettings
{
    public int Id { get; set; }

    /// <summary>Must match the id in the OCPP WebSocket path /ocpp/1.6/{chargePointId}.</summary>
    public string ChargePointId { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = "Charger";

    /// <summary>Max charge power, used for scheduling math only (no OCPP current limiting).</summary>
    public double MaxChargeKw { get; set; } = 11;

    public int ConnectorId { get; set; } = 1;

    // --- Charge-power slider (dashboard) → OCPP SetChargingProfile ---

    /// <summary>Lower bound of the dashboard charge-power slider (kW).</summary>
    public double ChargePowerMinKw { get; set; } = 1.4;

    /// <summary>Upper bound of the dashboard charge-power slider (kW).</summary>
    public double ChargePowerMaxKw { get; set; } = 11;

    /// <summary>
    /// Last charge-power ceiling applied via SetChargingProfile (kW). Written only by the
    /// set-power endpoint — the settings PUT does not touch it — and used to seed the slider.
    /// </summary>
    public double ChargePowerSetpointKw { get; set; } = 11;

    /// <summary>Nominal per-phase supply voltage (V). Used to convert the kW setpoint to amps for the OCPP profile.</summary>
    public double SupplyVoltage { get; set; } = 230;

    /// <summary>Number of phases the charger draws on. Drives the kW→A conversion and the profile's numberPhases.</summary>
    public int PhaseCount { get; set; } = 3;

    // --- Pushed to the charger on connect (ChangeConfiguration) ---

    /// <summary>Seconds; sent as the BootNotification response Interval.</summary>
    public int HeartbeatInterval { get; set; } = 60;

    /// <summary>Seconds between sampled MeterValues; drives per-hour cost granularity.</summary>
    public int MeterValueSampleInterval { get; set; } = 10;

    public int ClockAlignedDataInterval { get; set; } = 10;

    /// <summary>CSV of OCPP measurands for MeterValuesSampledData / MeterValuesAlignedData.</summary>
    public string MeterValuesSampledData { get; set; } =
        "Power.Active.Import,Energy.Active.Import.Register,Current.Import,Voltage,Current.Offered,Power.Offered,SoC,Voltage.L1,Voltage.L2,Voltage.L3";
}
